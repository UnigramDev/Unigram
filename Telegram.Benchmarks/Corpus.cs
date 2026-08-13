//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Telegram.Benchmarks
{
    public sealed class Payload
    {
        public string Name { get; set; } = string.Empty;

        // UTF-8, NUL-terminated, exactly as td_receive hands it over.
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        public int Length => Bytes.Length - 1;

        public override string ToString() => $"{Name} ({Length:N0}B)";
    }

    public static class Corpus
    {
        // Every *.jsonl in Corpus\ - one payload per line. Real captures dropped in here take
        // precedence over the synthetic set; see README for how to capture them.
        public static List<Payload> Load()
        {
            var payloads = new List<Payload>();

            // Embedded, so the UWP host gets the same corpus without a file system to walk.
            foreach (var name in typeof(Corpus).Assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var real = !name.Contains("synthetic");

                using var stream = typeof(Corpus).Assembly.GetManifestResourceStream(name)!;
                using var text = new StreamReader(stream);

                while (text.ReadLine() is { } line)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var buffer = new byte[Encoding.UTF8.GetByteCount(line) + 1];
                    Encoding.UTF8.GetBytes(line, 0, line.Length, buffer, 0);

                    payloads.Add(new Payload
                    {
                        Name = (real ? "" : "~") + TypeOf(line),
                        Bytes = buffer
                    });
                }
            }

            if (payloads.Count == 0)
            {
                throw new InvalidOperationException("No payloads embedded; check the csproj EmbeddedResource items");
            }

            return payloads;
        }

        // Kept for a host that would rather read loose files than rebuild to pick up a capture.
        public static List<Payload> LoadFromDisk()
        {
            var dir = FindCorpusDirectory();
            var payloads = new List<Payload>();

            foreach (var path in Directory.GetFiles(dir, "*.jsonl").OrderBy(x => x))
            {
                var real = !Path.GetFileName(path).StartsWith("synthetic", StringComparison.OrdinalIgnoreCase);

                foreach (var line in System.IO.File.ReadLines(path))
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var bytes = new byte[Encoding.UTF8.GetByteCount(line) + 1];
                    Encoding.UTF8.GetBytes(line, 0, line.Length, bytes, 0);

                    payloads.Add(new Payload
                    {
                        Name = (real ? "" : "~") + TypeOf(line),
                        Bytes = bytes
                    });
                }
            }

            if (payloads.Count == 0)
            {
                throw new InvalidOperationException($"No payloads found in {dir}");
            }

            return payloads;
        }

        private static string TypeOf(string line)
        {
            const string marker = "\"@type\":\"";

            var start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return "unknown";
            }

            start += marker.Length;
            var end = line.IndexOf('"', start);
            return end < 0 ? "unknown" : line.Substring(start, end - start);
        }

        private static string FindCorpusDirectory()
        {
            var dir = AppContext.BaseDirectory;

            while (dir != null)
            {
                var candidate = Path.Combine(dir, "Corpus");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }

            throw new DirectoryNotFoundException("Corpus directory not found");
        }
    }
}
