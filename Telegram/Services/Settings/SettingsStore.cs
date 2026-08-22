//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.Storage;

namespace Telegram.Services.Settings
{
    // The only thing in the app that knows where a setting is stored. An implementation that does
    // not depend on the app container can be dropped in here without touching a single accessor.
    public interface ISettingsStore
    {
        bool TryGetValue(string key, out object value);
        void SetValue(string key, object value);
        bool ContainsKey(string key);
        void Remove(string key);
        void Clear();

        IEnumerable<string> ContainerNames { get; }
        ISettingsStore GetContainer(string name);
        bool TryGetContainer(string name, out ISettingsStore container);
        void DeleteContainer(string name);

        // ApplicationData persists as it goes. A file-backed store will not, and needs a save
        // point that does not depend on guessing when the last write happened.
        void Flush();
    }

    public static class SettingsStoreExtensions
    {
        public static T GetValueOrDefault<T>(this ISettingsStore store, string key, T defaultValue)
        {
            return store.TryGet(key, out T value) ? value : defaultValue;
        }

        // A value stored as the wrong type used to throw on the cast. Falling back to the default
        // is the safer answer for something read on the way up.
        public static bool TryGet<T>(this ISettingsStore store, string key, out T value)
        {
            if (store.TryGetValue(key, out object stored) && stored is T result)
            {
                value = result;
                return true;
            }

            value = default;
            return false;
        }

        public static void AddOrUpdateValue<T>(this ISettingsStore store, ref T storage, string key, T value)
        {
            storage = value;
            store.SetValue(key, value);
        }
    }

    public partial class ApplicationDataSettingsStore : ISettingsStore
    {
        private readonly ApplicationDataContainer _container;

        private static ISettingsStore _local;
        public static ISettingsStore Local => _local ??= new ApplicationDataSettingsStore(ApplicationData.Current.LocalSettings);

        private ApplicationDataSettingsStore(ApplicationDataContainer container)
        {
            _container = container;
        }

        public bool TryGetValue(string key, out object value)
        {
            return _container.Values.TryGetValue(key, out value);
        }

        public void SetValue(string key, object value)
        {
            _container.Values[key] = value;
        }

        public bool ContainsKey(string key)
        {
            return _container.Values.ContainsKey(key);
        }

        public void Remove(string key)
        {
            _container.Values.Remove(key);
        }

        public void Clear()
        {
            _container.Values.Clear();
        }

        public IEnumerable<string> ContainerNames => _container.Containers.Keys;

        public ISettingsStore GetContainer(string name)
        {
            return new ApplicationDataSettingsStore(_container.CreateContainer(name, ApplicationDataCreateDisposition.Always));
        }

        public bool TryGetContainer(string name, out ISettingsStore container)
        {
            if (_container.Containers.TryGetValue(name, out var existing))
            {
                container = new ApplicationDataSettingsStore(existing);
                return true;
            }

            container = null;
            return false;
        }

        public void DeleteContainer(string name)
        {
            _container.DeleteContainer(name);
        }

        public void Flush()
        {
        }
    }

    public partial class SettingsServiceBase
    {
        protected readonly ISettingsStore _container;

        public SettingsServiceBase(string key)
            : this(ApplicationDataSettingsStore.Local.GetContainer(key))
        {

        }

        public SettingsServiceBase(ISettingsStore container = null)
        {
            _container = container ?? ApplicationDataSettingsStore.Local;
        }

        public void AddOrUpdateValue(string key, object value)
        {
            _container.SetValue(key, value);
        }

        public void AddOrUpdateValue<T>(ref T storage, string key, T value)
        {
            _container.AddOrUpdateValue(ref storage, key, value);
        }

        protected void AddOrUpdateValue<T>(ref T storage, ISettingsStore container, string key, T value)
        {
            container.AddOrUpdateValue(ref storage, key, value);
        }

        protected void AddOrUpdateValue(ISettingsStore container, string key, object value)
        {
            container.SetValue(key, value);
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            return _container.GetValueOrDefault(key, defaultValue);
        }

        protected valueType GetValueOrDefault<valueType>(ISettingsStore container, string key, valueType defaultValue)
        {
            return container.GetValueOrDefault(key, defaultValue);
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return _container.TryGet(key, out value);
        }

        protected static bool TryGetValue<T>(ISettingsStore container, string key, out T value)
        {
            return container.TryGet(key, out value);
        }

        public virtual void Clear()
        {
            _container.Clear();
        }
    }
}
