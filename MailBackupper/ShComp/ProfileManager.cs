using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShComp
{
    /// <summary>
    /// PrivateProfile形式のファイルに対して、値を読み込んだり、書き込んだりするメソッドを提供します。
    /// </summary>
    class ProfileManager
    {
        private string _fileName;

        public string AppName { get; set; }

        public ProfileManager(string fileName, string appName)
        {
            _fileName = fileName;
            AppName = appName;
        }

        public void Set(string keyName, string value)
        {
            SetString(_fileName, AppName, keyName, value);
        }

        public void Set(string keyName, object value)
        {
            Set(keyName, value.ToString());
        }

        public void Set(string keyName, string[] value)
        {
            Set(keyName, value.Length.ToString());
            for (int i = 0; i < value.Length; i++)
            {
                Set(keyName + i, value[i]);
            }
        }

        public void Set<T>(string keyName, T[] value)
        {
            Set(keyName, value.Length.ToString());
            for (int i = 0; i < value.Length; i++)
            {
                Set(keyName + i, value[i]);
            }
        }

        public string GetString(string keyName, string def = null)
        {
            return GetString(_fileName, AppName, keyName, def);
        }

        public string[] GetStrings(string keyName)
        {
            return GetArray(keyName, t => GetString(t));
        }

        public int GetInt32(string keyName, int def = 0)
        {
            return GetInt32(_fileName, AppName, keyName, def);
        }

        public int[] GetInt32s(string keyName)
        {
            return GetArray(keyName, t => GetInt32(t));
        }

        public bool GetBoolean(string keyName, bool def = false)
        {
            return Get(keyName, Convert.ToBoolean, def);
        }

        public bool[] GetBooleans(string keyName)
        {
            return GetArray(keyName, t => GetBoolean(t));
        }

        public DateTime GetDateTime(string keyName, DateTime def)
        {
            return Get(keyName, Convert.ToDateTime, def);
        }

        public DateTime[] GetDateTimes(string keyName, DateTime def)
        {
            return GetArray(keyName, t => GetDateTime(t, def));
        }

        public T Get<T>(string keyName, Func<string, T> converter, T def)
        {
            var result = GetString(keyName);
            if (result == null) return def;
            return converter(result);
        }

        public T[] GetArray<T>(string keyName, Func<string, T> getter)
        {
            var count = GetInt32(keyName);
            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = getter(keyName + i);
            }

            return result;
        }

        [DllImport("KERNEL32.DLL")]
        private static extern uint GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            uint nSize,
            string lpFileName);

        [DllImport("KERNEL32.DLL")]
        private static extern int GetPrivateProfileInt(
            string lpAppName,
            string lpKeyName,
            int nDefault,
            string lpFileName);

        [DllImport("KERNEL32.DLL")]
        private static extern uint WritePrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpString,
            string lpFileName);

        public static string GetString(string fileName, string appName, string keyName, string def)
        {
            var str = new StringBuilder(1024);
            GetPrivateProfileString(appName, keyName, def, str, 1024, fileName);
            return str.ToString();
        }

        public static int GetInt32(string fileName, string appName, string keyName, int def)
        {
            return GetPrivateProfileInt(appName, keyName, def, fileName);
        }

        public static void SetString(string fileName, string appName, string keyName, string value)
        {
            WritePrivateProfileString(appName, keyName, value, fileName);
        }

        public static dynamic Prepare(string fileName)
        {
            return new DynamicProfileManager(fileName);
        }

        private class DynamicProfileManager : DynamicObject
        {
            private string _fileName;

            public DynamicProfileManager(string fileName)
            {
                _fileName = fileName;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                result = new __I(_fileName, binder.Name);
                return true;
            }

            private class __I : DynamicObject
            {
                private string _fileName;
                private string _appName;

                public __I(string fileName, string appName)
                {
                    _fileName = fileName;
                    _appName = appName;
                }

                public override bool TrySetMember(SetMemberBinder binder, object value)
                {
                    var v = value as object[];
                    if (v != null)
                    {
                        ProfileManager.SetString(_fileName, _appName, binder.Name, v.Length.ToString());
                        for (int i = 0; i < v.Length; i++)
                        {
                            ProfileManager.SetString(_fileName, _appName, binder.Name + i, v[i].ToString());
                        }
                        return true;
                    }

                    ProfileManager.SetString(_fileName, _appName, binder.Name, value.ToString());
                    return true;
                }

                public override bool TryGetMember(GetMemberBinder binder, out object result)
                {
                    result = new __V(_fileName, _appName, binder.Name, null);
                    return true;
                }

                public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
                {
                    var arg = args.Length == 0 ? null : args[0];
                    result = new __V(_fileName, _appName, binder.Name, arg);
                    return true;
                }
            }

            private class __V : DynamicObject
            {
                private string _fileName;
                private string _appName;
                private string _keyName;
                private object _default;

                public __V(string fileName, string appName, string keyName, object def)
                {
                    _fileName = fileName;
                    _appName = appName;
                    _keyName = keyName;
                    _default = def;
                }

                public override bool TryConvert(ConvertBinder binder, out object result)
                {
                    if (binder.Type == typeof(int))
                    {
                        result = ProfileManager.GetInt32(_fileName, _appName, _keyName, (int)(_default ?? 0));
                        return true;
                    }

                    if (binder.Type == typeof(dynamic[]))
                    {
                        var count = ProfileManager.GetInt32(_fileName, _appName, _keyName, 0);
                        var r = new dynamic[count];
                        for (int i = 0; i < count; i++)
                        {
                            r[i] = new __V(_fileName, _appName, _keyName + i, null);
                        }
                        result = r;
                        return true;
                    }

                    var v = ProfileManager.GetString(_fileName, _appName, _keyName, (string)_default);

                    if (binder.Type == typeof(string))
                    {
                        result = v;
                        return true;
                    }
                    else if (binder.Type == typeof(bool))
                    {
                        result = Convert.ToBoolean(v);
                        return true;
                    }

                    result = null;
                    return false;
                }
            }
        }
    }
}
