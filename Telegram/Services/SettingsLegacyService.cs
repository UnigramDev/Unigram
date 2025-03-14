//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using Telegram.Navigation.Services;
using Windows.Foundation;
using Windows.Storage;

namespace Telegram.Services
{
    public interface ISettingsLegacyService
    {
        NavigationState Values { get; }
        bool Exists(string key);
        T Read<T>(string key, T fallback = default);
        void Remove(string key);
        void Write<T>(string key, T value);
        ISettingsLegacyService Open(string folderName, bool createFolderIsNotExists = true);
        void Clear(bool deleteSubContainers = true);

        bool IsBasicType(object parameter);
    }

    // https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-SettingsService
    public partial class SettingsLegacyService : ISettingsLegacyService
    {
        /// <summary>
        /// Creates an <c>ISettingsService</c> object targeting the requested (optional) <paramref name="folderName"/>
        /// in the <paramref name="strategy"/> container.
        /// </summary>
        /// <param name="strategy">Roaming or Local</param>
        /// <param name="folderName">Name of the settings folder to use</param>
        /// <param name="createFolderIfNotExists"><c>true</c> to create the folder if it isn't already there, false otherwise.</param>
        /// <returns></returns>
        public static ISettingsLegacyService Create(string folderName = null, bool createFolderIfNotExists = true)
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
            return new SettingsLegacyService(new NavigationState());
        }

        public NavigationState Values { get; private set; }

        private static readonly ConcurrentDictionary<string, NavigationState> _keys = new ConcurrentDictionary<string, NavigationState>();

        private SettingsLegacyService(NavigationState values)
        {
            Values = values;
        }

        public ISettingsLegacyService Open(string folderName, bool createFolderIfNotExists = true)
        {
            if (!_keys.TryGetValue(folderName, out NavigationState values))
            {
                _keys[folderName] = values = new NavigationState();
            }

            var service = new SettingsLegacyService(values);
            return service;
        }

        public bool Exists(string key) => Values.ContainsKey(key);

        public void Remove(string key)
        {
            Values.Remove(key);

            _keys.TryRemove(key, out _);
        }

        public void Clear(bool deleteSubContainers = true)
        {
            Values.Clear();

            if (deleteSubContainers)
            {
                _keys.Clear();
            }
        }

        public void Write<T>(string key, T value)
        {
            //var type = typeof(T);
            //if (value != null)
            //{
            //    type = value.GetType();
            //}
            //var converter = Converters.GetConverter(type);
            //var container = new Dictionary<string, object>();
            //var converted = converter.ToStore(value, type);
            //if (converted != null)
            //{
            //    var valueLength = converted.Length;
            //    if (valueLength > MaxValueSize)
            //    {
            //        int count = (valueLength - 1) / MaxValueSize + 1;
            //        container["Count"] = count;
            //        for (int part = 0; part < count; part++)
            //        {
            //            string partValue = converted.Substring(part * MaxValueSize, Math.Min(MaxValueSize, valueLength));
            //            container["Part" + part] = partValue;
            //            valueLength = valueLength - MaxValueSize;
            //        }
            //    }
            //    else
            //    {
            //        container["Value"] = converted;
            //    }
            //}
            //if ((type != typeof(string) && !type.GetTypeInfo().IsValueType) || (type != typeof(T)))
            //{
            //    container["Type"] = type.AssemblyQualifiedName;
            //}
            Values[key] = value;
        }

        public T Read<T>(string key, T fallback = default)
        {
            try
            {
                if (Values.ContainsKey(key))
                {
                    var container = Values[key];
                    if (container is T converted)
                    {
                        return converted;
                    }
                    //var type = typeof(T);
                    //if (container.ContainsKey("Type"))
                    //{
                    //    type = Type.GetType((string)container["Type"]);
                    //}
                    //string value = null;
                    //if (container.ContainsKey("Value"))
                    //{
                    //    value = container["Value"] as string;
                    //}
                    //else if (container.ContainsKey("Count"))
                    //{
                    //    int count = (int)container["Count"];
                    //    var sb = new StringBuilder(count * MaxValueSize);
                    //    for (int statePart = 0; statePart < count; statePart++)
                    //    {
                    //        sb.Append(container["Part" + statePart]);
                    //    }
                    //    value = sb.ToString();
                    //}
                    //var converter = Converters.GetConverter(type);
                    //var converted = (T)converter.FromStore(value, type);
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

            if (parameter is sbyte or short or ushort or int or uint or long or ulong or float or double)
            {
                return true;
            }
            else if (parameter is bool)
            {
                return true;
            }
            else if (parameter is char or string)
            {
                return true;
            }
            else if (parameter is DateTime or TimeSpan)
            {
                return true;
            }
            else if (parameter is Guid or Point or Size or Rect)
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
