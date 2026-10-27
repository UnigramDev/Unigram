//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Telegram.Common;
using Windows.Storage;

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
            : FileSettingsStore.Open(Path.Combine(ApplicationDataEx.LocalPath, "settings.bin"));
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

        public void Flush()
        {
        }
    }

#if UNPACKAGED
    /// <summary>
    /// The unpackaged half: the same container tree, held in memory and written to one file.
    /// <see cref="ApplicationData"/> persists as it goes and this cannot, so the write is
    /// debounced rather than deferred to <see cref="Flush"/> alone - a setting the user changed
    /// and a crash two seconds later must not add up to a setting never changed.
    /// </summary>
    public partial class FileSettingsStore : ISettingsStore
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

        private static readonly TimeSpan _debounce = TimeSpan.FromSeconds(2);

        private readonly Dictionary<string, object> _values = new();
        private readonly Dictionary<string, FileSettingsStore> _containers = new();

        // Containers are handed out as separate instances but are one tree and one file, so every
        // node takes the root's lock rather than its own.
        private readonly FileSettingsStore _root;
        private readonly object _lock;

        private readonly string _path;
        private Timer _timer;
        private bool _dirty;

        public static ISettingsStore Open(string path)
        {
            var store = new FileSettingsStore(path);
            store.Load();
            return store;
        }

        private FileSettingsStore(string path)
        {
            _root = this;
            _lock = new object();
            _path = path;
        }

        private FileSettingsStore(FileSettingsStore root)
        {
            _root = root;
            _lock = root._lock;
        }

        public bool TryGetValue(string key, out object value)
        {
            lock (_lock)
            {
                return _values.TryGetValue(key, out value);
            }
        }

        public void SetValue(string key, object value)
        {
            lock (_lock)
            {
                _values[key] = value;
                Invalidate();
            }
        }

        public bool ContainsKey(string key)
        {
            lock (_lock)
            {
                return _values.ContainsKey(key);
            }
        }

        public void Remove(string key)
        {
            lock (_lock)
            {
                if (_values.Remove(key))
                {
                    Invalidate();
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (_values.Count > 0)
                {
                    _values.Clear();
                    Invalidate();
                }
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_values.Keys);
                }
            }
        }

        public IEnumerable<string> ContainerNames
        {
            get
            {
                lock (_lock)
                {
                    // A copy: the caller enumerates outside the lock, and one of them deletes the
                    // sessions it walks.
                    return new List<string>(_containers.Keys);
                }
            }
        }

        public ISettingsStore GetContainer(string name)
        {
            lock (_lock)
            {
                if (!_containers.TryGetValue(name, out var container))
                {
                    _containers[name] = container = new FileSettingsStore(_root);
                    Invalidate();
                }

                return container;
            }
        }

        public bool TryGetContainer(string name, out ISettingsStore container)
        {
            lock (_lock)
            {
                if (_containers.TryGetValue(name, out var existing))
                {
                    container = existing;
                    return true;
                }

                container = null;
                return false;
            }
        }

        public void DeleteContainer(string name)
        {
            lock (_lock)
            {
                if (_containers.Remove(name))
                {
                    Invalidate();
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                if (!_root._dirty)
                {
                    return;
                }

                _root._dirty = false;

                try
                {
                    _root.Save();
                }
                catch
                {
                    // Still dirty, so the next flush - the timer's or the caller's - tries again.
                    _root._dirty = true;
                }
            }
        }

        // The caller holds the lock.
        private void Invalidate()
        {
            _root._dirty = true;
            _root._timer ??= new Timer(static state => ((FileSettingsStore)state).Flush(), _root, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _root._timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }

        private void Save()
        {
            var temp = _path + ".tmp";

            using (var stream = File.Create(temp))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(Format);
                Write(writer, this);
            }

            File.Move(temp, _path, overwrite: true);
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }

                using var stream = File.OpenRead(_path);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != Magic || reader.ReadByte() != Format)
                {
                    return;
                }

                Read(reader, this);
            }
            catch
            {
                // A half-written or unreadable file leaves the app on its defaults rather than
                // refusing to start, and the file is kept: the next flush overwrites it, and until
                // then it is the only copy of what the settings were.
                _values.Clear();
                _containers.Clear();
            }
        }

        private static void Write(BinaryWriter writer, FileSettingsStore container)
        {
            writer.Write(container._values.Count);

            foreach (var value in container._values)
            {
                writer.Write(value.Key);
                WriteValue(writer, value.Value);
            }

            writer.Write(container._containers.Count);

            foreach (var child in container._containers)
            {
                writer.Write(child.Key);
                Write(writer, child.Value);
            }
        }

        private static void Read(BinaryReader reader, FileSettingsStore container)
        {
            var values = reader.ReadInt32();

            for (int i = 0; i < values; i++)
            {
                var key = reader.ReadString();
                container._values[key] = ReadValue(reader);
            }

            var containers = reader.ReadInt32();

            for (int i = 0; i < containers; i++)
            {
                var name = reader.ReadString();
                var child = new FileSettingsStore(container._root);

                Read(reader, child);
                container._containers[name] = child;
            }
        }

        // The set is closed on purpose: an unknown tag cannot be skipped, so a value that does not
        // round-trip has to fail here rather than take the whole file down on the next load.
        private static void WriteValue(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    writer.Write(TagNull);
                    break;
                case bool boolean:
                    writer.Write(TagBoolean);
                    writer.Write(boolean);
                    break;
                case int int32:
                    writer.Write(TagInt32);
                    writer.Write(int32);
                    break;
                case uint uint32:
                    writer.Write(TagUInt32);
                    writer.Write(uint32);
                    break;
                case long int64:
                    writer.Write(TagInt64);
                    writer.Write(int64);
                    break;
                case ulong uint64:
                    writer.Write(TagUInt64);
                    writer.Write(uint64);
                    break;
                case float single:
                    writer.Write(TagSingle);
                    writer.Write(single);
                    break;
                case double doubl:
                    writer.Write(TagDouble);
                    writer.Write(doubl);
                    break;
                case string text:
                    writer.Write(TagString);
                    writer.Write(text);
                    break;
                case byte[] bytes:
                    writer.Write(TagBytes);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    break;
                default:
                    throw new NotSupportedException($"{value.GetType()} cannot be stored in the settings file");
            }
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
