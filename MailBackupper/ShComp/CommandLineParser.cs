using System.Collections.Generic;
using System.Linq;

namespace ShComp
{
    static class CommandLineParser
    {
        public static IEnumerable<KeyValuePair<string, string>> Parse(string[] args, string prefix = "-")
        {
            int count = 0;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith(prefix))
                    {
                        yield return new KeyValuePair<string, string>(args[i], args[i + 1]);
                        i++;
                    }
                    else
                    {
                        yield return new KeyValuePair<string, string>(args[i], null);
                    }
                }
                else
                {
                    yield return new KeyValuePair<string, string>(count++.ToString(), args[i]);
                }
            }
        }

        public static Dictionary<string, string> ToDictionary(this IEnumerable<KeyValuePair<string, string>> pairs)
        {
            return pairs.ToDictionary(t => t.Key, t => t.Value);
        }

        public static string GetOption(this Dictionary<string, string> opts, string key, string def)
        {
            string data;
            if (opts.TryGetValue(key, out data)) return data;

            return def;
        }
    }
}
