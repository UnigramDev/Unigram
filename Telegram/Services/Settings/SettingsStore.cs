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
}
