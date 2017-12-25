using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace Telegram.Api.Helpers
{
    public static class SettingsHelper
    {
        private static readonly object SyncLock = new object();

        public static object GetValue(string key)
        {
            var path = FileUtils.GetFileName(key);

            lock (SyncLock)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var payload = File.ReadAllText(path);
                        var temp = JsonConvert.DeserializeObject(payload, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                        var obj = temp;

                        return obj;
                    }
                    catch { }
                }
            }

            return null;
        }

        public static void SetValue(string key, object value)
        {
            lock (SyncLock)
            {
                try
                {
                    var path = FileUtils.GetFileName(key);
                    var payload = JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                    File.WriteAllText(path + ".tmp", payload);
                    File.Copy(path + ".tmp", path, true);
                }
                catch { }
            }
        }

        public static void RemoveValue(string key)
        {
            lock (SyncLock)
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static readonly ApplicationDataContainer isolatedStore;

        static SettingsHelper()
        {
            try
            {
                isolatedStore = ApplicationData.Current.LocalSettings;
            }
            catch { }
        }

        private static bool AddOrUpdateValue(string key, Object value)
        {
            bool valueChanged = false;

            if (isolatedStore.Values.ContainsKey(key))
            {
                if (isolatedStore.Values[key] != value)
                {
                    isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        private static valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        private static int? _userId;
        public static int UserId
        {
            get
            {
                if (_userId == null)
                    _userId = GetValueOrDefault(SelectedAccount + "UserId", 0);

                return _userId ?? 0;
            }
            set
            {
                _userId = value;
                AddOrUpdateValue(SelectedAccount + "UserId", value);
            }
        }

        private static int? _selectedAccount;
        public static int SelectedAccount
        {
            get
            {
                if (_selectedAccount == null)
                    _selectedAccount = GetValueOrDefault("SelectedAccount", 0);

                return _selectedAccount ?? 0;
            }
            set
            {
                _selectedAccount = value;
                AddOrUpdateValue("SelectedAccount", value);
            }
        }

        public static int SwitchAccount
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("SwitchAccount"))
                {
                    return (int)ApplicationData.Current.LocalSettings.Values["SwitchAccount"];
                }


                return -1;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SwitchAccount"] = value;
            }
        }

        private static bool? _isAuthorized;
        public static bool IsAuthorized
        {
            get
            {
                if (_isAuthorized == null)
                    _isAuthorized = GetValueOrDefault("Authorized", false);

                return _isAuthorized ?? false;
            }
            set
            {
                _isAuthorized = value;
                AddOrUpdateValue("Authorized", value);
            }
        }

        public static string ChannelUri
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("ChannelUri"))
                {
                    return (string)ApplicationData.Current.LocalSettings.Values["ChannelUri"];
                }

                return null;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ChannelUri"] = value;
            }
        }

        public static int SupportedLayer
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("SupportedLayer"))
                {
                    return (int)ApplicationData.Current.LocalSettings.Values["SupportedLayer"];
                }

                return Constants.SupportedLayer;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SupportedLayer"] = value;
            }
        }

        public static int DatabaseVersion
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("DatabaseVersion"))
                {
                    return (int)ApplicationData.Current.LocalSettings.Values["DatabaseVersion"];
                }

                DatabaseVersion = Constants.DatabaseVersion;
                return Constants.DatabaseVersion;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["DatabaseVersion"] = value;
            }
        }

        public static bool IsTestMode
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsTestMode"))
                {
                    return (bool)ApplicationData.Current.LocalSettings.Values["IsTestMode"];
                }

                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["IsTestMode"] = value;
            }
        }

        #region Proxy

        public static void CleanUp()
        {
            _isProxyEnabled = null;
            _isCallsProxyEnabled = null;
            _proxyServer = null;
            _proxyPort = null;
            _proxyUsername = null;
            _proxyPassword = null;
        }

        private static bool? _isProxyEnabled;
        public static bool IsProxyEnabled
        {
            get
            {
                if (_isProxyEnabled == null)
                    _isProxyEnabled = GetValueOrDefault("ProxyEnabled", false);

                return _isProxyEnabled ?? false;
            }
            set
            {
                _isProxyEnabled = value;
                AddOrUpdateValue("ProxyEnabled", value);
            }
        }

        private static bool? _isCallsProxyEnabled;
        public static bool IsCallsProxyEnabled
        {
            get
            {
                if (_isCallsProxyEnabled == null)
                    _isCallsProxyEnabled = GetValueOrDefault("CallsProxyEnabled", false);

                return _isCallsProxyEnabled ?? false;
            }
            set
            {
                _isCallsProxyEnabled = value;
                AddOrUpdateValue("CallsProxyEnabled", value);
            }
        }

        private static string _proxyServer;
        public static string ProxyServer
        {
            get
            {
                if (_proxyServer == null)
                    _proxyServer = GetValueOrDefault<string>("ProxyServer", null);

                return _proxyServer;
            }
            set
            {
                _proxyServer = value;
                AddOrUpdateValue("ProxyServer", value);
            }
        }

        private static int? _proxyPort;
        public static int ProxyPort
        {
            get
            {
                if (_proxyPort == null)
                    _proxyPort = GetValueOrDefault("ProxyPort", 1080);

                return _proxyPort ?? 1080;
            }
            set
            {
                _proxyPort = value;
                AddOrUpdateValue("ProxyPort", value);
            }
        }

        private static string _proxyUsername;
        public static string ProxyUsername
        {
            get
            {
                if (_proxyUsername == null)
                    _proxyUsername = GetValueOrDefault<string>("ProxyUsername", null);

                return _proxyUsername;
            }
            set
            {
                _proxyUsername = value;
                AddOrUpdateValue("ProxyUsername", value);
            }
        }

        private static string _proxyPassword;
        public static string ProxyPassword
        {
            get
            {
                if (_proxyPassword == null)
                    _proxyPassword = GetValueOrDefault<string>("ProxyPassword", null);

                return _proxyPassword;
            }
            set
            {
                _proxyPassword = value;
                AddOrUpdateValue("ProxyPassword", value);
            }
        }

        #endregion
    }

    //public static class SettingsHelper
    //{
    //    private static readonly object SyncLock = new object();

    //    public static void CrossThreadAccess(Action<Dictionary<string, object>> action)
    //    {
    //        lock (SyncLock)
    //        {
    //            try
    //            {
    //                action(LocalSettings);
    //            }
    //            catch (Exception e)
    //            {
    //                Execute.ShowDebugMessage("SettingsHelper.CrossThreadAccess" + e);
    //            }
    //        }
    //    }

    //    public static T GetValue<T>(string key)
    //    {
    //        object result;
    //        lock (SyncLock) // critical for wp7 devices
    //        {
    //            try
    //            {
    //                if (LocalSettings.TryGetValue(key, out result))
    //                {
    //                    return (T)result;
    //                }

    //                result = default(T);
    //            }
    //            catch (Exception e)
    //            {
    //                Logs.Log.Write("SettingsHelper.GetValue " + e);
    //                result = default(T);
    //            }
    //        }
    //        return (T)result;
    //    }

    //    public static object GetValue(string key)
    //    {
    //        object result;
    //        lock (SyncLock) //critical for wp7 devices
    //        {
    //            try
    //            {
    //                if (LocalSettings.TryGetValue(key, out result))
    //                {
    //                    return result;
    //                }

    //                result = null;
    //            }
    //            catch (Exception e)
    //            {
    //                Logs.Log.Write("SettingsHelper.GetValue " + e);
    //                result = null;
    //            }

    //        }
    //        return result;
    //    }

    //    public static void SetValue(string key, object value)
    //    {
    //        lock (SyncLock)
    //        {
    //            LocalSettings[key] = value;
    //        }
    //    }

    //    public static void RemoveValue(string key)
    //    {
    //        lock (SyncLock)
    //        {
    //            LocalSettings.Remove(key);
    //        }
    //    }

    //    private static Dictionary<string, object> _settings;

    //    public static Dictionary<string, object> LocalSettings
    //    {
    //        get
    //        {
    //            if (_settings == null)
    //            {
    //                _settings = GetValuesAsync().Result;
    //            }

    //            return _settings;
    //        }
    //    }

    //    public static async Task<Dictionary<string, object>> GetValuesAsync()
    //    {
    //        try
    //        {
    //            using (var fileStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync("__ApplicationSettings"))
    //            {
    //                //var stringReader = new StreamReader(fileStream);
    //                //var str = stringReader.ReadToEnd();

    //                using (var streamReader = new StreamReader(fileStream))
    //                {
    //                    var line = streamReader.ReadLine() ?? string.Empty;

    //                    var knownTypes = line.Split('\0')
    //                        .Where(x => !string.IsNullOrEmpty(x))
    //                        .Select(Type.GetType)
    //                        .ToList();

    //                    ReplaceNonPclTypes(knownTypes);

    //                    fileStream.Position = line.Length + Environment.NewLine.Length;

    //                    var serializer = new DataContractSerializer(typeof(Dictionary<string, object>), knownTypes);
    //                    return (Dictionary<string, object>)serializer.ReadObject(fileStream);
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Logs.Log.Write("SettingsHelper.GetValuesAsync exception " + ex);

    //            return new Dictionary<string, object>();
    //        }
    //    }

    //    private static void ReplaceNonPclTypes(List<Type> knownTypes)
    //    {
    //        //for (var i = 0; i < knownTypes.Count; i++)
    //        //{
    //        //    if (knownTypes[i].Name == typeof(BackgroundItem).Name)
    //        //    {
    //        //        knownTypes[i] = typeof(BackgroundItem);
    //        //    }
    //        //    else if (knownTypes[i].Name == typeof(TLConfig28).Name)
    //        //    {
    //        //        knownTypes[i] = typeof(TLConfig28);
    //        //    }
    //        //}
    //    }
    //}
}