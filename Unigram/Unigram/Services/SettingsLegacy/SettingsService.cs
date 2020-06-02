using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Windows.Foundation;
using Windows.Storage;

namespace Unigram.Services.SettingsLegacy
{
    // https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-SettingsService
    public class SettingsService : ISettingsService
    {
        /// <summary>
        /// Creates an <c>ISettingsService</c> object targeting the requested (optional) <paramref name="folderName"/>
        /// in the <paramref name="strategy"/> container.
        /// </summary>
        /// <param name="strategy">Roaming or Local</param>
        /// <param name="folderName">Name of the settings folder to use</param>
        /// <param name="createFolderIfNotExists"><c>true</c> to create the folder if it isn't already there, false otherwise.</param>
        /// <returns></returns>
        public static ISettingsService Create(string folderName = null, bool createFolderIfNotExists = true)
        {
            //ApplicationDataContainer rootContainer;
            //switch (strategy)
            //{
            //    case SettingsStrategies.Local:
            //        rootContainer = ApplicationData.Current.LocalSettings;
            //        break;
            //    case SettingsStrategies.Roam:
            //        rootContainer = ApplicationData.Current.RoamingSettings;
            //        break;
            //    default:
            //        throw new ArgumentException($"Unsupported Settings Strategy: {strategy}", nameof(strategy));
            //}

            //ApplicationDataContainer targetContainer = rootContainer;
            //if (!string.IsNullOrWhiteSpace(folderName))
            //{
            //    try
            //    {
            //        targetContainer = rootContainer.CreateContainer(folderName, createFolderIfNotExists ? ApplicationDataCreateDisposition.Always : ApplicationDataCreateDisposition.Existing);
            //    }
            //    catch (Exception)
            //    {
            //        throw new KeyNotFoundException($"No folder exists named '{folderName}'");
            //    }
            //}

            //return new SettingsService(targetContainer);
            return new SettingsService(new Dictionary<string, object>());
        }

        public IDictionary<string, object> Values { get; private set; }

        public IPropertyMapping Converters { get; set; } = new JsonMapping();

        private static Dictionary<string, IDictionary<string, object>> _keys = new Dictionary<string, IDictionary<string, object>>();

        private SettingsService(IDictionary<string, object> values)
        {
            Values = values;
        }

        public ISettingsService Open(string folderName, bool createFolderIfNotExists = true)
        {
            IDictionary<string, object> values;
            if (!_keys.TryGetValue(folderName, out values))
            {
                _keys[folderName] = values = new Dictionary<string, object>();
            }

            var service = new SettingsService(values);
            service.Converters = Converters;
            return service;
        }

        public bool Exists(string key) => Values.ContainsKey(key);

        public void Remove(string key)
        {
            if (Values.ContainsKey(key))
                Values.Remove(key);
            if (_keys.ContainsKey(key))
                _keys.Remove(key);
        }

        public void Clear(bool deleteSubContainers = true)
        {
            Values.Clear();
            if (deleteSubContainers)
            {
                foreach (var container in _keys.ToArray())
                {
                    _keys.Remove(container.Key);
                }
            }
        }

        const int MaxValueSize = 8000;

        public void Write<T>(string key, T value)
        {
            var type = typeof(T);
            if (value != null)
            {
                type = value.GetType();
            }
            var converter = Converters.GetConverter(type);
            var container = new ApplicationDataCompositeValue();
            var converted = converter.ToStore(value, type);
            if (converted != null)
            {
                var valueLength = converted.Length;
                if (valueLength > MaxValueSize)
                {
                    int count = (valueLength - 1) / MaxValueSize + 1;
                    container["Count"] = count;
                    for (int part = 0; part < count; part++)
                    {
                        string partValue = converted.Substring(part * MaxValueSize, Math.Min(MaxValueSize, valueLength));
                        container["Part" + part] = partValue;
                        valueLength = valueLength - MaxValueSize;
                    }
                }
                else
                    container["Value"] = converted;
            }
            if ((type != typeof(string) && !type.GetTypeInfo().IsValueType) || (type != typeof(T)))
            {
                container["Type"] = type.AssemblyQualifiedName;
            }
            Values[key] = container;
        }

        public T Read<T>(string key, T fallback = default(T))
        {
            try
            {
                if (Values.ContainsKey(key))
                {
                    var container = Values[key] as ApplicationDataCompositeValue;
                    var type = typeof(T);
                    if (container.ContainsKey("Type"))
                    {
                        type = Type.GetType((string)container["Type"]);
                    }
                    string value = null;
                    if (container.ContainsKey("Value"))
                    {
                        value = container["Value"] as string;
                    }
                    else if (container.ContainsKey("Count"))
                    {
                        int count = (int)container["Count"];
                        var sb = new StringBuilder(count * MaxValueSize);
                        for (int statePart = 0; statePart < count; statePart++)
                        {
                            sb.Append(container["Part" + statePart]);
                        }
                        value = sb.ToString();
                    }
                    var converter = Converters.GetConverter(type);
                    var converted = (T)converter.FromStore(value, type);
                    return converted;
                }
                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public bool IsBasicType(object parameter)
        {
            // https://docs.microsoft.com/en-us/windows/uwp/design/app-settings/store-and-retrieve-app-data
            // Some of these types are surely supported by ApplicationDataContainer,
            // but most likely not by Frame.

            if (parameter is sbyte || parameter is Int16 || parameter is UInt16 || parameter is Int32 || parameter is UInt32 || parameter is Int64 || parameter is UInt64 || parameter is Single || parameter is Double)
            {
                return true;
            }
            else if (parameter is bool)
            {
                return true;
            }
            else if (parameter is char || parameter is string)
            {
                return true;
            }
            else if (parameter is DateTime || parameter is TimeSpan)
            {
                return true;
            }
            else if (parameter is Guid || parameter is Point || parameter is Size || parameter is Rect)
            {
                return true;
            }
            else if (parameter is ApplicationDataCompositeValue)
            {
                return true;
            }

            return false;
        }
    }
}
