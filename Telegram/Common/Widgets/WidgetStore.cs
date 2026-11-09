//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Windows.Data.Json;

namespace Telegram.Common.Widgets
{
    public readonly struct WidgetKey : IEquatable<WidgetKey>
    {
        public WidgetKey(long chatId, long messageId)
        {
            ChatId = chatId;
            MessageId = messageId;
        }

        public long ChatId { get; }
        public long MessageId { get; }

        public bool Equals(WidgetKey other) => ChatId == other.ChatId && MessageId == other.MessageId;
        public override bool Equals(object obj) => obj is WidgetKey other && Equals(other);
        public override int GetHashCode() => ChatId.GetHashCode() * 31 + MessageId.GetHashCode();
    }

    // What the client keeps per widget message (docs/sdk-v1.md §5.4, §5.6, §5.7): its Web Storage, local
    // on disk and session in memory, the content height the bubble opens at, and the crashes in a row.
    // One per account, keyed by (chat, message) under it; any thread.
    public sealed class WidgetStore
    {
        private static readonly Dictionary<int, WidgetStore> _sessions = new();

        public static WidgetStore For(int sessionId)
        {
            lock (_sessions)
            {
                if (!_sessions.TryGetValue(sessionId, out WidgetStore store))
                {
                    _sessions[sessionId] = store = new WidgetStore(Path.Combine(AppPaths.LocalPath, $"{sessionId}", "widgets"));
                }

                return store;
            }
        }

        // The app suspending or terminating.
        public static void FlushAll()
        {
            WidgetStore[] stores;
            lock (_sessions)
            {
                stores = new WidgetStore[_sessions.Count];
                _sessions.Values.CopyTo(stores, 0);
            }

            foreach (var store in stores)
            {
                store.Flush();
            }
        }

        // How long a local commit waits for the next one before it goes to disk.
        private const int WriteDelayMs = 1000;

        private sealed class Entry
        {
            // Null until read from disk.
            public Dictionary<string, string> Local;
            public Dictionary<string, string> Session;
            public bool Dirty;
            public double Height;
            public int Crashes;
            // The HTML the crashes are of: new HTML starts the count again.
            public ulong CrashHash;
        }

        private readonly string _directory;
        private readonly object _lock = new object();
        // Held across a whole flush and a delete, so a write cannot land after the delete of its file.
        private readonly object _diskLock = new object();
        private readonly Dictionary<WidgetKey, Entry> _entries = new Dictionary<WidgetKey, Entry>();
        private readonly Timer _timer;
        private bool _scheduled;

        private WidgetStore(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
            _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        // The storage a widget is created with: copies, filled into the host's maps.
        public void Load(WidgetKey key, IDictionary<string, string> local, IDictionary<string, string> session)
        {
            lock (_lock)
            {
                var entry = GetOrCreate(key);
                if (entry.Local == null)
                {
                    entry.Local = Read(key);
                }

                foreach (var pair in entry.Local)
                {
                    local[pair.Key] = pair.Value;
                }

                if (entry.Session != null)
                {
                    foreach (var pair in entry.Session)
                    {
                        session[pair.Key] = pair.Value;
                    }
                }
            }
        }

        // The new full content of an area, from the widget's host thread.
        public void Commit(WidgetKey key, string area, IReadOnlyDictionary<string, string> values)
        {
            var copy = new Dictionary<string, string>(values.Count);
            foreach (var pair in values)
            {
                copy[pair.Key] = pair.Value;
            }

            lock (_lock)
            {
                var entry = GetOrCreate(key);
                if (area == "session")
                {
                    entry.Session = copy;
                    return;
                }

                entry.Local = copy;
                entry.Dirty = true;

                if (!_scheduled)
                {
                    _scheduled = true;
                    _timer.Change(WriteDelayMs, Timeout.Infinite);
                }
            }
        }

        // Every pending local area to disk now: a widget hidden or destroyed, the app suspending.
        public void Flush()
        {
            lock (_diskLock)
            {
                var pending = new List<KeyValuePair<WidgetKey, Dictionary<string, string>>>();
                lock (_lock)
                {
                    _scheduled = false;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);

                    foreach (var pair in _entries)
                    {
                        if (pair.Value.Dirty)
                        {
                            pair.Value.Dirty = false;
                            pending.Add(new KeyValuePair<WidgetKey, Dictionary<string, string>>(pair.Key, pair.Value.Local));
                        }
                    }
                }

                // Commit replaces an area's dictionary rather than changing it, so these are safe to read unlocked.
                foreach (var pair in pending)
                {
                    try
                    {
                        Write(pair.Key, pair.Value);
                    }
                    catch (IOException)
                    {
                        // The account's folder went with the account, while one of its widgets was still committing.
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_diskLock)
            {
                lock (_lock)
                {
                    _entries.Clear();
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
                    {
                        TryDelete(file);
                    }
                }
                catch (IOException)
                {
                    // No folder, no files.
                }
            }
        }

        public bool TryGetHeight(WidgetKey key, out double height)
        {
            lock (_lock)
            {
                height = _entries.TryGetValue(key, out Entry entry) ? entry.Height : 0;
                return height > 0;
            }
        }

        public void SetHeight(WidgetKey key, double height)
        {
            lock (_lock)
            {
                GetOrCreate(key).Height = height;
            }
        }

        // Crashes in a row of this HTML: an edit with new HTML starts over (docs/sdk-v1.md §5.7).
        public int Crashes(WidgetKey key, ulong hash)
        {
            lock (_lock)
            {
                return _entries.TryGetValue(key, out Entry entry) && entry.CrashHash == hash ? entry.Crashes : 0;
            }
        }

        public void AddCrash(WidgetKey key, ulong hash)
        {
            lock (_lock)
            {
                var entry = GetOrCreate(key);
                entry.Crashes = entry.CrashHash == hash ? entry.Crashes + 1 : 1;
                entry.CrashHash = hash;
            }
        }

        public void ResetCrashes(WidgetKey key)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out Entry entry))
                {
                    entry.Crashes = 0;
                }
            }
        }

        // For the debug listing: messages with stored data on disk, and their bytes.
        public string Describe()
        {
            int files = 0;
            long bytes = 0;
            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                files++;
                bytes += new FileInfo(file).Length;
            }

            return string.Format("widget storage: {0} on disk, {1:F1} KB", files, bytes / 1024.0);
        }

        private Entry GetOrCreate(WidgetKey key)
        {
            if (!_entries.TryGetValue(key, out Entry entry))
            {
                _entries[key] = entry = new Entry();
            }

            return entry;
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        private string PathOf(WidgetKey key)
        {
            return Path.Combine(_directory, key.ChatId + "_" + key.MessageId + ".json");
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Held open by an antivirus or the indexer: it goes with the next Clear.
            }
        }

        private Dictionary<string, string> Read(WidgetKey key)
        {
            var area = new Dictionary<string, string>();
            var path = PathOf(key);
            if (!File.Exists(path))
            {
                return area;
            }

            try
            {
                var json = JsonObject.Parse(File.ReadAllText(path));
                foreach (var pair in json.GetNamedObject("local"))
                {
                    area[pair.Key] = pair.Value.GetString();
                }
            }
            catch (Exception)
            {
                // A file torn by a crash mid-write: the widget starts empty, as a new one would.
                area.Clear();
            }

            return area;
        }

        private void Write(WidgetKey key, Dictionary<string, string> local)
        {
            var path = PathOf(key);
            if (local.Count == 0)
            {
                File.Delete(path);
                return;
            }

            var area = new JsonObject();
            foreach (var pair in local)
            {
                area[pair.Key] = JsonValue.CreateStringValue(pair.Value);
            }

            var json = new JsonObject();
            json["local"] = area;

            var temp = path + ".tmp";
            File.WriteAllText(temp, json.Stringify());
            File.Delete(path);
            File.Move(temp, path);
        }
    }
}
