using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShComp.Net.Mail
{
    class ImapClient : IDisposable
    {
        private TcpClient _client;
        private Stream _stream;
        private StreamWriter _writer;

        private int _next;
        private static Dictionary<string, TaskCompletionSource<_MailResponse>> _tcs = new Dictionary<string, TaskCompletionSource<_MailResponse>>();

        public bool IsConnected { get; private set; }

        public bool IsDisposed { get; private set; }

        public ImapClient()
        {
            _client = new TcpClient();
        }

        public async Task Connect(string host, int port, bool useSsl)
        {
            if (IsConnected) throw new InvalidOperationException();
            IsConnected = true;

            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            if (useSsl)
            {
                var sslStream = new SslStream(_stream, false, (_, __, ___, ____) => true);
                await sslStream.AuthenticateAsClientAsync(host);
                _stream = sslStream;
            }

            _writer = new StreamWriter(_stream) { AutoFlush = true };
            var _____ = Task.Run((Action)RunGetResponses);
        }

        private Task<_MailResponse> SendCommand(string command)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().ToString());

            var id = (_next++).ToString();
            var tcs = _tcs[id] = new TaskCompletionSource<_MailResponse>();
            var data = id + " " + command;
            Console.WriteLine("-> " + data);
            var _ = _writer.WriteLineAsync(data);
            return tcs.Task;
        }

        public Task Login(string username, string password)
        {
            return SendCommand($"LOGIN {username} {password}");
        }

        public Task SelectBox(string name)
        {
            return SendCommand("SELECT " + name);
        }

        private Regex _searchAllRegex = new Regex(@"(\d+)");

        public async Task<IEnumerable<string>> GetMailIds()
        {
            var result = await SendCommand("SEARCH ALL");
            var ms = _searchAllRegex.Matches(result.Message.Split('\n')[0]);
            return ms.Cast<Match>().Select(m => m.Value);
        }

        private static Regex _hr = new Regex(@"^(.+?):\s*<?(.+?)>?\s*$", RegexOptions.Multiline);

        public async Task<string> GetMessageId(string id)
        {
            var result = await SendCommand($"FETCH {id} BODY.PEEK[HEADER.FIELDS (MESSAGE-ID)]");
            var data = Encoding.ASCII.GetString(result.Data);
            var m = _hr.Match(data);

            if (m.Groups[1].Value.Equals("MESSAGE-ID", StringComparison.OrdinalIgnoreCase))
            {
                var mid = m.Groups[2].Value;
                if (mid == null || mid.IndexOf("?") != -1)
                {
                    throw new Exception("MESSAGE-IDが不正");
                }
                return mid;
            }

            throw new Exception();
        }

        public async Task WriteBody(Stream stream, string id)
        {
            var result = await SendCommand($"FETCH {id} BODY.PEEK[]");
            if (result.Data == null || result.Data.Length == 0) throw new Exception("メッセージの取得失敗");
            await stream.WriteAsync(result.Data, 0, result.Data.Length);
        }

        public async Task<byte[]> GetBody(string id)
        {
            var result = await SendCommand($"FETCH {id} BODY.PEEK[]");
            if (result.Data == null || result.Data.Length == 0) throw new Exception("メッセージの取得失敗");
            return result.Data;
        }

        private Regex _fr = new Regex(@"(\\\w+)");

        public async Task<IEnumerable<string>> GetFlags(string id)
        {
            var result = await SendCommand($"FETCH {id} FLAGS");
            var ms = _fr.Matches(result.Message);
            return ms.Cast<Match>().Select(m => m.Value);
        }

        public async Task AddFlags(string id, IEnumerable<string> flags)
        {
            var result = await SendCommand($"STORE {id} +FLAGS ({string.Join(" ", flags)})");
        }

        public async Task Expunge()
        {
            var result = await SendCommand($"EXPUNGE");
        }

        private Regex _dr = new Regex("\"(.+)\"");
        public async Task<DateTime> GetInternalDate(string id)
        {
            var result = await SendCommand($"FETCH {id} INTERNALDATE");
            var m = _dr.Match(result.Message);
            if (!m.Success) throw new Exception();
            return DateTime.Parse(m.Groups[1].Value);
        }

        private void RunGetResponses()
        {
            var sr = new Regex(@"^(\d+) (.+)$");
            var br = new Regex(@"{(\d+)}$");
            var sb = new StringBuilder();
            var ms = new MemoryStream();
            byte[] exv = null;
            try
            {
                while (true)
                {
                    var data = _stream.ReadByte();
                    if (data == -1)
                    {
                        break;
                    }

                    if (data == 13) continue;
                    if (data == 10)
                    {
                        var line = Encoding.ASCII.GetString(ms.GetBuffer(), 0, (int)ms.Position);
                        ms.Position = 0;

                        Console.WriteLine("<- " + line);

                        if (line[0] == '*')
                        {
                            var res = line.Substring(2);
                            sb.AppendLine(res);
                            var bm = br.Match(res);
                            if (bm.Success)
                            {
                                var count = int.Parse(bm.Groups[1].Value);
                                exv = new byte[count];
                                var i = 0;

                                while (count - i != 0)
                                {
                                    i += _stream.Read(exv, i, count - i);
                                }

                                sb.AppendLine(line);
                            }
                            continue;
                        }

                        var m = sr.Match(line);
                        if (m.Success)
                        {
                            var id = m.Groups[1].Value;
                            var tcs = _tcs[id];
                            _tcs.Remove(id);

                            if (m.Groups[2].Value[0] != 'O') throw new Exception("ERR");

                            if (sb.Length == 0)
                            {
                                tcs.TrySetResult(new _MailResponse(m.Groups[2].Value, exv));
                                exv = null;
                            }
                            else
                            {
                                sb.Append(m.Groups[2].Value);
                                tcs.TrySetResult(new _MailResponse(sb.ToString(), exv));
                                exv = null;
                                sb.Clear();
                            }
                        }

                        continue;
                    }

                    ms.WriteByte((byte)data);
                }
            }
            catch { }

            Console.WriteLine("## END");
            Dispose();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            _stream?.Dispose();
            _client.Close();

            foreach (var tcs in _tcs)
            {
                tcs.Value.TrySetException(new Exception());
            }
        }

        private class _MailResponse
        {
            public string Message { get; }

            public byte[] Data { get; }

            public _MailResponse(string message, byte[] data)
            {
                Message = message;
                Data = data;
            }
        }
    }
}
