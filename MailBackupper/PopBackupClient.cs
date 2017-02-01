using ActiveUp.Net.Mail;
using System.Collections.Generic;

namespace MailBackupper
{
    class PopBackupClient : IBackupMailClient
    {
        private Pop3Client _c;

        public PopBackupClient()
        {
            _c = new Pop3Client();
        }

        public void Connect(string host, int port, string userName, string password, bool useSsl = false)
        {
            if (useSsl) _c.ConnectSsl(host, port, userName, password);
            else _c.Connect(host, port, userName, password);
        }

        public IBackupMailbox GetMailbox(string name)
        {
            return new _Box(_c);
        }

        private class _Box : IBackupMailbox
        {
            private Pop3Client _c;

            public string Name { get { return "pop"; } }

            public _Box(Pop3Client c)
            {
                _c = c;
            }

            public IEnumerable<int> GetMailIds()
            {
                for (int i = 1; i <= _c.MessageCount; i++)
                {
                    yield return i;
                }
            }

            public BackupMailHeader GetHeader(int mailId)
            {
                var header = _c.RetrieveHeaderObject(mailId);
                return new BackupMailHeader(header.ReceivedDate, header.MessageId);
            }

            public byte[] GetMessage(int mailId)
            {
                return _c.RetrieveMessage(mailId);
            }

            public void DeleteMessage(int mailId)
            {
                _c.DeleteMessage(mailId);
            }

            public bool HasFlag(int mailId, string name)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _c.Disconnect();
            _c.Close();
        }

        public void Expunge() { }
    }
}
