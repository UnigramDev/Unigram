//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.Storage;
#if UNPACKAGED
using System.IO;
using System.Text;
using Telegram.Common;
using UnpackagedApplicationData = Microsoft.Windows.Storage.ApplicationData;
using UnpackagedApplicationDataContainer = Microsoft.Windows.Storage.ApplicationDataContainer;
using UnpackagedCreateDisposition = Microsoft.Windows.Storage.ApplicationDataCreateDisposition;
#endif

namespace Telegram.Services.Settings
{
    // Which of the two implementations below the app runs on, decided once. Nothing else in the
    // app is allowed to name one of them: an accessor that reaches for ApplicationDataSettingsStore
    // by name is an accessor that crashes on an unpackaged launch.
    public static class SettingsStore
    {
#if UNPACKAGED
        private static ISettingsStore _local;
        public static ISettingsStore Local => _local ??= ApiInfo.HasPackageIdentity
            ? ApplicationDataSettingsStore.Local
            : UnpackagedSettingsStore.Open(Path.Combine(AppPaths.LocalPath, "settings.bin"));
#else
        public static ISettingsStore Local => ApplicationDataSettingsStore.Local;
#endif
    }

    // The only thing in the app that knows where a setting is stored. An implementation that does
    // not depend on the app container can be dropped in here without touching a single accessor.
    public interface ISettingsStore
    {
        bool TryGetValue(string key, out object value);
        void SetValue(string key, object value);
        bool ContainsKey(string key);
        void Remove(string key);
        void Clear();

        IEnumerable<string> Keys { get; }

        IEnumerable<string> ContainerNames { get; }
        ISettingsStore GetContainer(string name);
        bool TryGetContainer(string name, out ISettingsStore container);
        void DeleteContainer(string name);
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

        public IEnumerable<string> Keys => _container.Values.Keys;

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
    }

#if UNPACKAGED
    /// <summary>
    /// The unpackaged half, over the Windows App SDK's <see cref="UnpackagedApplicationData"/>:
    /// a registry key per container and a value per setting, under
    /// <c>HKCU\Software\Classes\Local Settings\Software\TelegramFZ-LLC\Windows</c>. That path is a
    /// servicing fix behind a feature switch: with the switch off, the same key sits under
    /// <c>HKCU\Software</c> and the settings read as reset. Each value is written with a type of
    /// its own, so it reads back as what was stored, as it does packaged, and each write reaches
    /// the registry before it returns: there is nothing to flush.
    /// </summary>
    public partial class UnpackagedSettingsStore : ISettingsStore
    {
        private const string Publisher = "TelegramFZ-LLC";

        // AppPaths keeps a profile of its own for debug builds, and the settings go with it.
#if DEBUG
        private const string Product = "Windows.Debug";
#else
        private const string Product = "Windows";
#endif

        private readonly UnpackagedApplicationDataContainer _container;

        // Values builds a new map over the key on every read.
        private readonly IDictionary<string, object> _values;

        public static ISettingsStore Open(string legacyPath)
        {
            var store = new UnpackagedSettingsStore(UnpackagedApplicationData.GetForUnpackaged(Publisher, Product).LocalSettings);
            SettingsFileImport.Import(legacyPath, store);
            return store;
        }

        private UnpackagedSettingsStore(UnpackagedApplicationDataContainer container)
        {
            _container = container;
            _values = container.Values;
        }

        public bool TryGetValue(string key, out object value)
        {
            return _values.TryGetValue(key, out value);
        }

        public void SetValue(string key, object value)
        {
            _values[key] = value;
        }

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<string> ContainerNames => _container.Containers.Keys;

        public ISettingsStore GetContainer(string name)
        {
            return new UnpackagedSettingsStore(_container.CreateContainer(name, UnpackagedCreateDisposition.Always));
        }

        public bool TryGetContainer(string name, out ISettingsStore container)
        {
            if (_container.Containers.TryGetValue(name, out var existing))
            {
                container = new UnpackagedSettingsStore(existing);
                return true;
            }

            container = null;
            return false;
        }

        public void DeleteContainer(string name)
        {
            _container.DeleteContainer(name);
        }
    }

    /// <summary>
    /// Reads the file the unpackaged settings were kept in before they moved to the registry,
    /// copies it in and moves it aside. Nothing writes the file any more, so it can go once every
    /// install has started on a build that has this.
    /// </summary>
    internal static class SettingsFileImport
    {
        private const uint Magic = 0x54535447;  // "GTST"
        private const byte Format = 1;

        private const byte TagNull = 0;
        private const byte TagBoolean = 1;
        private const byte TagInt32 = 2;
        private const byte TagUInt32 = 3;
        private const byte TagInt64 = 4;
        private const byte TagUInt64 = 5;
        private const byte TagSingle = 6;
        private const byte TagDouble = 7;
        private const byte TagString = 8;
        private const byte TagBytes = 9;

        private sealed class Node
        {
            public readonly Dictionary<string, object> Values = new();
            public readonly Dictionary<string, Node> Containers = new();
        }

        // No logging in here: this runs from AppSettings' static initializer, and the logger reads
        // AppSettings.
        public static void Import(string path, ISettingsStore target)
        {
            var backup = path + ".bak";

            if (!File.Exists(path) && !File.Exists(backup))
            {
                return;
            }

            // The backup is one save older. It is also the only copy when a save failed after
            // moving the old file aside but before moving the new one in.
            var source = TryRead(path, out var tree) ? path : TryRead(backup, out tree) ? backup : null;

            if (source != null)
            {
                // Replaced rather than merged: a file that is still here was written after the
                // registry was, by an older build.
                foreach (var name in target.ContainerNames)
                {
                    target.DeleteContainer(name);
                }

                target.Clear();
                Copy(tree, target);
            }

            // Only after the copy, so that a start that dies halfway through it imports again.
            Retire(path, path == source ? ".imported" : ".bad");
            Retire(backup, backup == source ? ".imported" : ".bad");
        }

        private static void Copy(Node node, ISettingsStore target)
        {
            foreach (var value in node.Values)
            {
                target.SetValue(value.Key, value.Value);
            }

            foreach (var child in node.Containers)
            {
                Copy(child.Value, target.GetContainer(child.Key));
            }
        }

        private static void Retire(string path, string suffix)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Move(path, path + suffix, overwrite: true);
                }
            }
            catch
            {
                // Left where it is, and the next start tries again.
            }
        }

        private static bool TryRead(string path, out Node tree)
        {
            try
            {
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    using var reader = new BinaryReader(stream, Encoding.UTF8);

                    if (reader.ReadUInt32() == Magic && reader.ReadByte() == Format)
                    {
                        tree = Read(reader);

                        if (stream.Position == stream.Length)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Nothing of a file that failed halfway is kept: the next one, or defaults.
            }

            tree = null;
            return false;
        }

        private static Node Read(BinaryReader reader)
        {
            var node = new Node();
            var values = reader.ReadInt32();

            for (int i = 0; i < values; i++)
            {
                var key = reader.ReadString();
                node.Values[key] = ReadValue(reader);
            }

            var containers = reader.ReadInt32();

            for (int i = 0; i < containers; i++)
            {
                var name = reader.ReadString();
                node.Containers[name] = Read(reader);
            }

            return node;
        }

        private static object ReadValue(BinaryReader reader)
        {
            var tag = reader.ReadByte();

            return tag switch
            {
                TagNull => null,
                TagBoolean => reader.ReadBoolean(),
                TagInt32 => reader.ReadInt32(),
                TagUInt32 => reader.ReadUInt32(),
                TagInt64 => reader.ReadInt64(),
                TagUInt64 => reader.ReadUInt64(),
                TagSingle => reader.ReadSingle(),
                TagDouble => reader.ReadDouble(),
                TagString => reader.ReadString(),
                TagBytes => reader.ReadBytes(reader.ReadInt32()),
                _ => throw new InvalidDataException($"Unknown settings value tag {tag}")
            };
        }
    }
#endif

    public partial class SettingsServiceBase
    {
        protected readonly ISettingsStore _container;

        public SettingsServiceBase(string key)
            : this(SettingsStore.Local.GetContainer(key))
        {

        }

        public SettingsServiceBase(ISettingsStore container = null)
        {
            _container = container ?? SettingsStore.Local;
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
