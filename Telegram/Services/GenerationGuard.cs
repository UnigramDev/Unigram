//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Windows.Storage;

namespace Telegram.Services
{
    /// <summary>
    /// Keeps a generation that crashes the app from crashing it on every launch, as TDLib
    /// resumes it each time. Each running generation leaves a marker on disk, removed when it
    /// ends and when the app is closed normally, so a marker still there when the same
    /// generation starts again means the last attempt died with the app. Native faults are fail-
    /// fasts no handler sees, so the marker is the only trace they leave.
    /// </summary>
    public static class GenerationGuard
    {
        private static readonly object _lock = new();
        // Each running generation's marker name, and how many attempts at it died before.
        private static readonly Dictionary<string, int> _running = new();

        private static string _folder;

        private static string GetPath(string name)
        {
            _folder ??= Path.Combine(ApplicationDataEx.LocalPath, "generations");
            return Path.Combine(_folder, name);
        }

        // Conversions run long, so the file is named after a hash of one.
        private static string GetName(string conversion)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(conversion));

            var builder = new StringBuilder(32);

            for (int i = 0; i < 16; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// How many attempts at this conversion in a row closed the app; 0 when the last one
        /// finished. The caller decides how many it tolerates, and calls <see cref="End"/>
        /// either way.
        /// </summary>
        public static int Begin(string conversion)
        {
            var crashes = 0;

            try
            {
                var name = GetName(conversion);
                var path = GetPath(name);

                lock (_lock)
                {
                    // The marker holds the crashes before the attempt that left it.
                    if (File.Exists(path))
                    {
                        int.TryParse(File.ReadAllText(path), out crashes);
                        crashes++;
                    }

                    _running[name] = crashes;

                    // The system keeps what was written even when the process dies.
                    Directory.CreateDirectory(_folder);
                    File.WriteAllText(path, crashes.ToString());
                }
            }
            catch (Exception ex)
            {
                // Without a marker the generation simply goes unguarded.
                Logger.Error(ex);
            }

            return crashes;
        }

        public static void End(string conversion)
        {
            try
            {
                var name = GetName(conversion);

                lock (_lock)
                {
                    _running.Remove(name);
                    File.Delete(GetPath(name));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        /// <summary>
        /// The app is closing normally: what runs now is not to blame for anything. Markers left
        /// by an earlier session are kept, as their generations have not been retried yet.
        /// </summary>
        public static void Suspend()
        {
            lock (_lock)
            {
                foreach (var name in _running.Keys)
                {
                    try
                    {
                        File.Delete(GetPath(name));
                    }
                    catch
                    {
                        // Left for the next launch to judge.
                    }
                }
            }
        }

        /// <summary>
        /// Generations that were running when the app was suspended carry on: they are guarded
        /// again.
        /// </summary>
        public static void Resume()
        {
            lock (_lock)
            {
                foreach (var running in _running)
                {
                    try
                    {
                        Directory.CreateDirectory(_folder);
                        File.WriteAllText(GetPath(running.Key), running.Value.ToString());
                    }
                    catch
                    {
                        // Unguarded until it ends.
                    }
                }
            }
        }
    }
}
