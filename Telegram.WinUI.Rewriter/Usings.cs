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
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Telegram.WinUI.Rewriter
{
    /// <summary>
    /// Writes Telegram\GlobalUsings.WinUI.cs: the Windows.UI.* to Microsoft.UI.* remap as global
    /// usings, so the WinUI flavour compiles the shared sources in place.
    ///
    /// It rests on two lookup rules. A global alias beats a namespace a file imports, so a type
    /// whose name survives in the old namespace - Windows.UI.Composition is still in the Windows
    /// SDK projection - is aliased by name and the stale import loses. And a namespace that no
    /// longer exists anywhere is declared empty, so the sources' own using directives still bind.
    ///
    /// What it cannot reach is listed by the report and handled in the sources: a qualified name,
    /// a file alias of the same name (CS1537 against the global one), or a static call the new type
    /// lacks.
    /// </summary>
    internal static class Usings
    {
        private static readonly Regex _import = new(@"^\s*using\s+([A-Za-z][A-Za-z0-9.]*)\s*;", RegexOptions.Multiline);
        private static readonly Regex _alias = new(@"^\s*(?:global\s+)?using\s+([A-Za-z_][A-Za-z0-9_]*)\s*=", RegexOptions.Multiline);

        /// <summary>
        /// args: &lt;source root&gt; &lt;output file&gt; &lt;assembly&gt;... An assembly prefixed
        /// with <c>old:</c> is one the flavour still references, the Windows SDK projection; the
        /// others are the Windows App SDK projections.
        /// </summary>
        public static int Generate(string root, string output, IEnumerable<string> assemblies)
        {
            var types = new Dictionary<string, HashSet<string>>();

            foreach (var argument in assemblies)
            {
                Read(argument.StartsWith("old:") ? argument[4..] : argument, types);
            }

            var imported = new SortedSet<string>(StringComparer.Ordinal);
            var fileAliases = new HashSet<string>();
            var generated = Path.GetFullPath(output);

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(@"\obj\") || file.Contains(@"\bin\") || string.Equals(Path.GetFullPath(file), generated, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = File.ReadAllText(file);

                foreach (Match match in _import.Matches(text))
                {
                    imported.Add(match.Groups[1].Value);
                }

                foreach (Match match in _alias.Matches(text))
                {
                    fileAliases.Add(match.Groups[1].Value);
                }
            }

            // Whole-namespace moves, then the types that moved out of a namespace that stayed.
            var moves = new List<(string From, string To, Func<string, bool> Filter)>();

            foreach (var ns in imported.Where(x => x == "Windows.UI.Xaml" || x.StartsWith("Windows.UI.Xaml.")))
            {
                // Interop's types are ABI ones and did not move; Direct has no equivalent at all,
                // and is stood in for by Telegram.Compat.Direct below.
                if (ns != "Windows.UI.Xaml.Interop" && ns != "Windows.UI.Xaml.Core.Direct")
                {
                    moves.Add((ns, "Microsoft.UI.Xaml" + ns["Windows.UI.Xaml".Length..], null));
                }
            }

            foreach (var ns in imported.Where(x => x == "Windows.UI.Composition" || x.StartsWith("Windows.UI.Composition.")))
            {
                moves.Add((ns, "Microsoft.UI.Composition" + ns["Windows.UI.Composition".Length..], null));
            }

            moves.Add(("Windows.UI.Text", "Microsoft.UI.Text", null));
            moves.Add(("Windows.UI.Input", "Microsoft.UI.Input", null));
            moves.Add(("Windows.System", "Microsoft.UI.Dispatching", x => x.StartsWith("DispatcherQueue")));
            moves.Add(("Windows.Devices.Input", "Microsoft.UI.Input", x => x == "PointerDeviceType"));
            // CoreText's classes need a CoreWindow; Telegram.Native.Text has them over TSF, and
            // leaves the enums and CoreTextRange to the Windows namespace.
            moves.Add(("Windows.UI.Text.Core", "Telegram.Native.Text", null));

            var imports = new List<string>();
            var stubs = new List<string>();
            var aliases = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var report = new List<string>();

            foreach (var (from, to, filter) in moves)
            {
                if (!types.ContainsKey(to))
                {
                    report.Add($"MISSING {to} (from {from})");

                    if (filter == null && !types.ContainsKey(from))
                    {
                        stubs.Add(from);
                    }

                    continue;
                }

                // Importing Shapes everywhere would make System.IO.Path ambiguous in every file
                // that names it, so its types are aliased one by one and Path goes through XamlPath.
                if (to == "Microsoft.UI.Xaml.Shapes")
                {
                    foreach (var name in types[to].Where(x => x != "Path"))
                    {
                        aliases[name] = $"{to}.{name}";
                    }

                    if (!types.ContainsKey(from))
                    {
                        stubs.Add(from);
                    }

                    continue;
                }

                if (filter == null)
                {
                    imports.Add(to);
                }

                if (!types.ContainsKey(from))
                {
                    if (filter == null)
                    {
                        stubs.Add(from);
                    }

                    continue;
                }

                // The old namespace is real here too, so every name both have is aliased, or a file
                // still importing it gets CS0104 - or the old type, where the new one lacks the name.
                foreach (var name in types[to].Where(x => types[from].Contains(x) && (filter == null || filter(x))))
                {
                    if (aliases.TryGetValue(name, out var previous))
                    {
                        report.Add($"CLASH {name}: {previous} and {to}.{name}, kept the first");
                    }
                    else if (fileAliases.Contains(name))
                    {
                        report.Add($"SKIPPED {name}: a file aliases it, which is CS1537 against a global alias");
                    }
                    else
                    {
                        aliases[name] = $"{to}.{name}";
                    }
                }
            }

            stubs.Add("Windows.UI.Xaml.Core.Direct");
            imports.Add("Telegram.Compat.Direct");

            // Compat\XamlDirect.WinUI.cs: a handle is the object itself.
            aliases["IXamlDirectObject"] = "System.Object";
            // No WinUI 3 equivalent; see Compat\CaptureElement.WinUI.cs.
            aliases["CaptureElement"] = "Telegram.Compat.CaptureElement";
            // Windows.UI.Colors is projected with UWP XAML, which this flavour does not reference,
            // so there is no shared name to find.
            aliases["Colors"] = "Microsoft.UI.Colors";
            // Microsoft.UI.Text.ITextDocument is not public: RichEditBox.Document returns the class.
            aliases["ITextDocument"] = "Microsoft.UI.Text.RichEditTextDocument";
            // Microsoft.UI.Xaml has types of these names too, and the app's predate them.
            aliases["WindowActivatedEventArgs"] = "Telegram.Navigation.WindowActivatedEventArgs";
            aliases["WindowSizeChangedEventArgs"] = "Telegram.Navigation.WindowSizeChangedEventArgs";
            aliases["LaunchActivatedEventArgs"] = "Windows.ApplicationModel.Activation.LaunchActivatedEventArgs";

            // A name two global imports both supply is CS0104 in any file that names it bare.
            var owners = new Dictionary<string, List<string>>();

            foreach (var ns in imports.Where(types.ContainsKey))
            {
                foreach (var name in types[ns])
                {
                    if (!owners.TryGetValue(name, out var list))
                    {
                        owners[name] = list = new List<string>();
                    }

                    list.Add(ns);
                }
            }

            foreach (var owner in owners.Where(x => x.Value.Count > 1 && !aliases.ContainsKey(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                report.Add($"AMBIGUOUS {owner.Key}: {string.Join(", ", owner.Value)}");
            }

            var builder = new StringBuilder();
            builder.Append("// <auto-generated/>\r\n");
            builder.Append("// Telegram.WinUI.Rewriter --usings. Regenerate it, do not edit it. Read from:\r\n");

            foreach (var argument in assemblies)
            {
                builder.Append($"//   {Package(argument)}\r\n");
            }

            builder.Append("\r\n");

            foreach (var ns in imports.Distinct())
            {
                builder.Append($"global using {ns};\r\n");
            }

            builder.Append("\r\n");

            foreach (var (name, target) in aliases)
            {
                builder.Append($"global using {name} = {target};\r\n");
            }

            builder.Append("\r\n");

            // Empty, so the sources' own using directives still resolve.
            foreach (var ns in stubs.Distinct())
            {
                builder.Append($"namespace {ns} {{ }}\r\n");
            }

            File.WriteAllText(output, builder.ToString(), new UTF8Encoding(true));

            Console.WriteLine($"{imports.Distinct().Count()} imports, {aliases.Count} aliases, {stubs.Distinct().Count()} stubs");

            foreach (var line in report)
            {
                Console.WriteLine(line);
            }

            return 0;
        }

        /// <summary>
        /// The path from the package cache down, so the header names the versions it was read from
        /// without naming anybody's profile directory.
        /// </summary>
        private static string Package(string argument)
        {
            var path = argument.Replace('\\', '/');
            var index = path.IndexOf("/packages/", StringComparison.OrdinalIgnoreCase);

            return index < 0
                ? Path.GetFileName(path)
                : (argument.StartsWith("old:") ? "old:" : "") + path[(index + "/packages/".Length)..];
        }

        private static void Read(string path,Dictionary<string, HashSet<string>> types)
        {
            using var reader = new PEReader(File.OpenRead(path));
            var metadata = reader.GetMetadataReader();

            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);
                if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
                {
                    continue;
                }

                // An open generic cannot be aliased, and none of the moved types is one.
                var name = metadata.GetString(type.Name);
                if (name.Contains('`'))
                {
                    continue;
                }

                var ns = metadata.GetString(type.Namespace);
                if (!types.TryGetValue(ns, out var set))
                {
                    types[ns] = set = new HashSet<string>();
                }

                set.Add(name);
            }
        }
    }
}
