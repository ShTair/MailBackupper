using ShComp.Net.Mail;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MailBackupper
{
    class ImapBackupClient : IBackupMailClient
    {
        private ImapClient _client;
        private string _currentBoxName;

        public ImapBackupClient()
        {
            _client = new ImapClient();
        }

        public void Connect(string host, int port, string userName, string password, bool useSsl = false)
        {
            _client.Connect(host, port, useSsl).Wait();
            _client.Login(userName, password).Wait();
        }

        public void Expunge()
        {
            _client.Expunge().Wait();
        }

        public IBackupMailbox GetMailbox(string name)
        {
            return new _Box(this, name);
        }

        private class _Box : IBackupMailbox
        {
            private ImapBackupClient _client;

            public string Name { get; }

            public _Box(ImapBackupClient client, string name)
            {
                _client = client;
                Name = name;
            }

            public void DeleteMessage(int mailId)
            {
                if (_client._currentBoxName != Name) _client._client.SelectBox(_client._currentBoxName = Name).Wait();
                _client._client.AddFlags(mailId.ToString(), new[] { "\\Deleted" }).Wait();
            }

            public BackupMailHeader GetHeader(int mailId)
            {
                if (_client._currentBoxName != Name) _client._client.SelectBox(_client._currentBoxName = Name).Wait();
                var date = _client._client.GetInternalDate(mailId.ToString()).Result;
                var mid = _client._client.GetMessageId(mailId.ToString()).Result;
                return new BackupMailHeader(date, mid);
            }

            public IEnumerable<int> GetMailIds()
            {
                if (_client._currentBoxName != Name) _client._client.SelectBox(_client._currentBoxName = Name).Wait();
                return _client._client.GetMailIds().Result.Select(t => int.Parse(t));
            }

            public byte[] GetMessage(int mailId)
            {
                if (_client._currentBoxName != Name) _client._client.SelectBox(_client._currentBoxName = Name).Wait();
                return _client._client.GetBody(mailId.ToString()).Result;
            }

            public bool HasFlag(int mailId, string name)
            {
                if (_client._currentBoxName != Name) _client._client.SelectBox(_client._currentBoxName = Name).Wait();
                return _client._client.GetFlags(mailId.ToString()).Result.Any(t => t.ToLower() == "\\" + name.ToLower());
            }
        }

        public void Dispose()
        {
            Expunge();
            _client.Dispose();
        }

        //private Imap4Client _c;

        //public ImapBackupClient()
        //{
        //    _c = new Imap4Client();
        //}

        //public void Connect(string host, int port, string userName, string password, bool useSsl = false)
        //{
        //    if (useSsl) _c.ConnectSsl(host, port);
        //    else _c.Connect(host, port);

        //    _c.Login(userName, password);
        //}

        //public IBackupMailbox GetMailbox(string name)
        //{
        //    var box = _c.SelectMailbox(name);
        //    return new _Box(box);
        //}

        //public void Expunge()
        //{
        //    _c.Expunge();
        //}

        //private class _Box : IBackupMailbox
        //{
        //    private Mailbox _b;

        //    public _Box(Mailbox b)
        //    {
        //        _b = b;
        //    }

        //    public string Name { get { return _b.Name; } }

        //    public IEnumerable<int> GetMailIds()
        //    {
        //        return _b.Search("ALL");
        //    }

        //    public Header GetHeader(int mailId)
        //    {
        //        return _b.Fetch.HeaderObject(mailId);
        //    }

        //    public byte[] GetMessage(int mailId)
        //    {
        //        var hasF = HasFlag(mailId, "seen");
        //        var m = _b.Fetch.Message(mailId);
        //        if (!hasF) _b.RemoveFlagsSilent(mailId, new FlagCollection() { "seen" });
        //        return m;
        //    }

        //    public void DeleteMessage(int mailId)
        //    {
        //        _b.DeleteMessage(mailId, false);
        //    }

        //    public bool HasFlag(int mailId, string name)
        //    {
        //        return _b.Fetch.Flags(mailId)[name] != null;
        //    }
        //}

        //public void Dispose()
        //{
        //    _c.Expunge();
        //    _c.Disconnect();
        //    _c.Close();
        //}
    }
}
