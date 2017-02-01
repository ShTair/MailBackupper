using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ActiveUp.Net.Mail;

namespace MailBackupper
{
    interface IBackupMailbox
    {
        string Name { get; }

        IEnumerable<int> GetMailIds();
        BackupMailHeader GetHeader(int mailId);
        byte[] GetMessage(int mailId);
        void DeleteMessage(int mailId);
        bool HasFlag(int mailId, string name);
    }

    class BackupMailHeader
    {
        public DateTime Date { get; }

        public string MessageId { get; }

        public BackupMailHeader(DateTime date, string messageId)
        {
            Date = date;
            MessageId = messageId;
        }
    }
}
