using ActiveUp.Net.Mail;
using Newtonsoft.Json;
using ShComp;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace MailBackupper
{
    class Program
    {
        private static bool _isDeletable;
        private static bool _isForce;

        static void Main(string[] args)
        {
            //var s1 = File.Open("text1.txt", FileMode.Append);
            //var s2 = File.Open("text2.txt", FileMode.Append);

            //using(var stream = new MultiStream(s1, s2))
            //using(var writer = new StreamWriter(stream))
            //{
            //    writer.WriteLine("test");
            //}

            //var config = ProfileManager.Prepare(Path.GetFullPath("config.ini")).MailBackupper;
            //dynamic[] paths = config.ConfigPath;

            var p = CommandLineParser.Parse(args).ToDictionary();
            var configPath = p["-c"];
            var backupPath = p["-b"];
            _isDeletable = p.ContainsKey("-d");
            var restPath = p["-r"];
            _isForce = p.ContainsKey("-f");

            Directory.GetFiles(configPath, "*.json", SearchOption.AllDirectories)
                .TryForeach(configFile => DoBackup(configFile, backupPath, restPath));
        }

        private static void DoBackup(string configFile, params string[] backupPaths)
        {
            var configStr = File.ReadAllText(configFile);
            dynamic config = JsonConvert.DeserializeObject(configStr);

            using (IBackupMailClient c = CreateClient(config))
            {
                c.Connect((string)config.Host, (int)config.Port,
                    (string)config.UserName, (string)config.Password, (bool?)config.IsSsl ?? false);

                ((IEnumerable<dynamic>)config.Boxes).TryForeach(boxConfig =>
                {
                    var box = c.GetMailbox((string)boxConfig.Name);
                    var limit = DateTime.Now.ToUniversalTime() - (TimeSpan)boxConfig.Span;
                    var dstBasePaths = Combine(backupPaths, (string)config.UserName, (string)boxConfig.Name);

                    box.GetMailIds().TryForeach(mailId =>
                    {
                        var header = box.GetHeader(mailId);
                        Console.WriteLine("{0:yyyy/MM/dd HH:mm:ss} {1}", header.Date, header.MessageId);
                        Console.WriteLine($"{header.Date:yyyy/MM/dd HH:mm:ss} {header.MessageId}");

                        var basePaths = Combine(dstBasePaths, header.Date.ToString("yyyy"), header.Date.ToString("yyyyMMdd"));
                        foreach (var basePath in basePaths)
                        {
                            if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                        }

                        var messageId = header.MessageId;
                        messageId = Regex.Replace(messageId, "[\\/:*?\"<>|]", "_");
                        var mailFiles = Combine(basePaths, string.Format("{0}.eml", messageId));
                        var mailFilesA = _isForce ? mailFiles : mailFiles.Where(t => !File.Exists(t));
                        if (mailFilesA.Any())
                        {
                            Console.WriteLine("Storing");
                            var mail = box.GetMessage(mailId);
                            if (mail == null || mail.Length == 0) throw new Exception();
                            foreach (var mailFile in mailFilesA)
                            {
                                File.WriteAllBytes(mailFile, mail);
                                File.SetCreationTime(mailFile, header.Date);
                                File.SetLastWriteTime(mailFile, header.Date);
                            }
                        }

                        if (_isDeletable && header.Date < limit && !box.HasFlag(mailId, "flagged"))
                        {
                            box.DeleteMessage(mailId);
                        }
                    });
                });
            }
        }

        private static IEnumerable<string> Combine(IEnumerable<string> dir, params string[] cm)
        {
            foreach (var item in dir)
            {
                yield return Path.Combine(Enumerable.Repeat(item, 1).Concat(cm).ToArray());
            }
        }

        private static IBackupMailClient CreateClient(dynamic config)
        {
            switch (((string)config.Type).ToLower())
            {
                case "imap": return new ImapBackupClient();
                case "pop": return new PopBackupClient();
            }

            throw new ArgumentException();
        }
    }

    static class Extensions
    {
        public static void TryForeach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                try { action(item); }
                catch (Exception exp) { Console.WriteLine(exp); }
            }
        }
    }
}
