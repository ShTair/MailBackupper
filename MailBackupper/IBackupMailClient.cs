using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailBackupper
{
    interface IBackupMailClient : IDisposable
    {
        void Connect(string host, int port, string userName, string password, bool useSsl = false);
        IBackupMailbox GetMailbox(string name);
        void Expunge();
    }
}
