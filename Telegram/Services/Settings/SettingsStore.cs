//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.Storage;
#if UNPACKAGED
using System;
using System.IO;
using System.Text;
using System.Threading;
using Telegram.Common;
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
            : FileSettingsStore.Open(Path.Combine(AppPaths.LocalPath, "settings.bin"));
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
    /// and a crash two seconds later must not add up to a setting never changed. The host flushes
    /// on the way out, because it ends the process without waiting for the timer.
    ///
    /// Each save is complete or absent: written to a temporary file, forced to disk, then swapped
    /// in with <see cref="File.Replace(string, string, string, bool)"/>, which keeps the previous
    /// file as a backup that <see cref="Load"/> falls back to.
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

        // Root only, and always taken before _lock: the tree is held only while a save serializes
        // it, never while the save waits on the disk. Saves queue on it, so an older snapshot can
        // never be written over a newer one.
        private readonly object _saveLock;

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
            _saveLock = new object();
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
            // Refused here, as ApplicationData refuses it, rather than when the file is next
            // written: one value that cannot be written would fail every save after it.
            if (!CanWrite(value))
            {
                throw new NotSupportedException($"{value.GetType()} cannot be stored in the settings file");
            }

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
            var root = _root;

            lock (root._saveLock)
            {
                byte[] data;

                lock (_lock)
                {
                    if (!root._dirty)
                    {
                        return;
                    }

                    data = root.Serialize();
                    root._dirty = false;
                }

                try
                {
                    root.Save(data);
                }
                catch (Exception ex)
                {
                    lock (_lock)
                    {
                        // Rescheduled as well as marked: without a change after this one, nothing
                        // else would ever try again.
                        root.Invalidate();
                    }

                    Logger.Error("Settings could not be saved", ex);
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

        // The caller holds the lock.
        private byte[] Serialize()
        {
            using var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(Format);
                Write(writer, this);
            }

            return stream.ToArray();
        }

        // The caller holds the save lock.
        private void Save(byte[] data)
        {
            var temp = _path + ".tmp";

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);

                // On the disk before the swap, not just in the cache: NTFS journals the rename but
                // not the data, so after a power cut the new name could point at zeros.
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, _path);
            }
        }

        // No logging in here: this runs from AppSettings' static initializer, and the logger reads
        // AppSettings. A file that could not be read is left beside the others as .bad instead.
        private void Load()
        {
            lock (_lock)
            {
                if (TryLoad(_path))
                {
                    return;
                }

                // There but unreadable, as opposed to absent: moved aside, so that the next save
                // cannot overwrite what may still be recoverable by hand.
                if (File.Exists(_path))
                {
                    try
                    {
                        File.Move(_path, _path + ".bad", overwrite: true);
                    }
                    catch
                    {
                        // Left where it is, and the next save replaces it.
                    }
                }

                // One save older. It is also the only copy when Replace failed after moving the
                // old file aside but before moving the new one in.
                if (TryLoad(_path + ".bak"))
                {
                    Invalidate();
                }
            }
        }

        private bool TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != Magic || reader.ReadByte() != Format)
                {
                    throw new InvalidDataException("Not a settings file");
                }

                Read(reader, this);

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Trailing data after the settings tree");
                }

                return true;
            }
            catch
            {
                // Nothing of a file that failed halfway is kept: defaults rather than a mix.
                _values.Clear();
                _containers.Clear();
                return false;
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

        // Exactly the types WriteValue has a tag for.
        private static bool CanWrite(object value)
        {
            return value is null or bool or int or uint or long or ulong or float or double or string or byte[];
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
