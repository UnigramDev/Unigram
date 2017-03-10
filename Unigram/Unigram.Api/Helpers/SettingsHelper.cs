using Newtonsoft.Json;
using System;
using System.IO;
using Windows.Storage;

namespace Telegram.Api.Helpers
{
    public static class SettingsHelper
    {
        private static readonly object SyncLock = new object();

        public static object GetValue(string key)
        {
            lock (SyncLock)
            {
                var path = FileUtils.GetFileName(key);
                if (File.Exists(path))
                {
                    using (var file = File.OpenText(path))
                    {
                        var payload = file.ReadToEnd();
                        var temp = JsonConvert.DeserializeObject(payload, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                        var obj = temp;

                        return obj;
                    }
                }
            }

            return null;
        }

        public static void SetValue(string key, object value)
        {
            lock (SyncLock)
            {
                var path = FileUtils.GetFileName(key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                using (var file = File.CreateText(path))
                {
                    //var type = Encoding.UTF8.GetBytes(value.GetType().FullName + Environment.NewLine);
                    //var serializer = new DataContractSerializer(value.GetType());
                    //file.Write(type, 0, type.Length);
                    //serializer.WriteObject(file, value);

                    var payload = JsonConvert.SerializeObject(value, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                    file.Write(payload);
                }
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

        public static int UserId
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("UserId"))
                {
                    return (int)ApplicationData.Current.LocalSettings.Values["UserId"];
                }

                return 0;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["UserId"] = value;
            }
        }

        public static string SessionGuid
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("SessionGuid"))
                {
                    return (string)ApplicationData.Current.LocalSettings.Values["SessionGuid"];
                }
                else
                {
                    SessionGuid = Guid.NewGuid().ToString();
                    return SessionGuid;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["SessionGuid"] = value;
            }
        }

        public static bool IsAuthorized
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsAuthorized"))
                {
                    return (bool)ApplicationData.Current.LocalSettings.Values["IsAuthorized"];
                }

                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["IsAuthorized"] = value;
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

                // TODO: maybe we have to remove - 1 when publishing in the store
                return Constants.DatabaseVersion - 1;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["DatabaseVersion"] = value;
            }
        }

        public static int GifsHash
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("GifsHash"))
                {
                    return (int)ApplicationData.Current.LocalSettings.Values["GifsHash"];
                }

                return 0;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["GifsHash"] = value;
            }
        }
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