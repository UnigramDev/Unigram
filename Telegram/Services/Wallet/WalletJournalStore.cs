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
using System.Threading.Tasks;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The engine's journal, held in one file and rewritten under a lock.
    /// </summary>
    /// <remarks>
    /// The journal records sends that have crossed their commit boundary, so losing a write can
    /// mean sending twice. Two things follow, and they are the reason this is not a dictionary
    /// with a save-on-idle:
    /// <list type="bullet">
    /// <item>compare and replace happen inside one lock, so a caller can never observe - or act
    /// on - a version that another caller is midway through replacing;</item>
    /// <item>the file is written aside and moved into place, so a crash mid-write leaves the
    /// previous journal intact rather than a truncated one.</item>
    /// </list>
    /// The file is not encrypted. It holds send state, not key material, and the app's local data
    /// is already scoped to the Windows user; encrypting it would add a failure mode to the one
    /// path that must not fail.
    /// </remarks>
    public sealed class WalletJournalStore : IWalletJournal
    {
        private const uint Magic = 0x4A4E524Cu; // "LRNJ"
        private const int Version = 1;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, WalletJournalEntry> _entries = new Dictionary<string, WalletJournalEntry>();
        private readonly string _path;

        private bool _loaded;

        public WalletJournalStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public async Task<WalletJournalEntry?> LoadAsync(string recordId, string slot)
        {
            await _lock.WaitAsync();
            try
            {
                Load();

                return _entries.TryGetValue(Key(recordId, slot), out var entry)
                    ? entry
                    : (WalletJournalEntry?)null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<WalletJournalExchange> CompareExchangeAsync(string recordId, string slot, ulong? expectedVersion, WalletJournalEntry replacement)
        {
            await _lock.WaitAsync();
            try
            {
                Load();

                var key = Key(recordId, slot);
                var present = _entries.TryGetValue(key, out var current);

                var matches = expectedVersion.HasValue
                    ? present && current.Version == expectedVersion.Value
                    : !present;

                if (!matches)
                {
                    return WalletJournalExchange.Rejected(present ? current : (WalletJournalEntry?)null);
                }

                _entries[key] = replacement;

                try
                {
                    Save();
                }
                catch
                {
                    // The write is the durable part of the contract. If it failed, the in-memory
                    // state must not claim otherwise, or the engine would believe a send is
                    // recorded that would not survive a restart.
                    if (present)
                    {
                        _entries[key] = current;
                    }
                    else
                    {
                        _entries.Remove(key);
                    }

                    throw;
                }

                return WalletJournalExchange.Replaced(replacement);
            }
            catch (WalletJournalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WalletJournalException(WalletJournalFailure.Unavailable, ex.GetType().Name);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            if (!File.Exists(_path))
            {
                return;
            }

            try
            {
                using var stream = File.OpenRead(_path);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
                {
                    throw new WalletJournalException(WalletJournalFailure.CorruptData, "unrecognized journal format");
                }

                var count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var version = reader.ReadUInt64();
                    var payload = reader.ReadBytes(reader.ReadInt32());

                    _entries[key] = new WalletJournalEntry(version, payload);
                }
            }
            catch (WalletJournalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Starting empty would silently discard a pending send, so a damaged journal is
                // reported rather than recovered from.
                _entries.Clear();
                throw new WalletJournalException(WalletJournalFailure.CorruptData, ex.GetType().Name);
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));

            var temporary = _path + ".tmp";

            using (var stream = File.Create(temporary))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(_entries.Count);

                foreach (var entry in _entries)
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Value.Version);
                    writer.Write(entry.Value.Payload.Length);
                    writer.Write(entry.Value.Payload);
                }

                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_path))
            {
                File.Replace(temporary, _path, null);
            }
            else
            {
                File.Move(temporary, _path);
            }
        }

        // The engine chooses both halves, and neither may contain the separator for this to be
        // reversible - but nothing here ever needs to reverse it, only to be unambiguous.
        private static string Key(string recordId, string slot)
        {
            return recordId.Length.ToString() + ":" + recordId + slot;
        }
    }
}
