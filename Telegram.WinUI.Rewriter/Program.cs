//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Telegram.WinUI.Rewriter
{
    /// <summary>
    /// The build-time half of the WinUI 3 flavour. The shared sources compile in place, through
    /// the global usings <c>--usings</c> writes (see <see cref="Usings"/>); what XAML cannot say
    /// with a using, two markup files, is patched into the flavour's obj tree.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The markup, which is a different problem from the sources and a much smaller one: the
        /// default XAML namespace URI is the same string in both frameworks and the muxc prefix
        /// resolves to the built-in controls, so all but two files are framework-agnostic already.
        /// Those two are not, and XAML has no <c>#if</c> - hence a patch per file.
        ///
        /// Every rule carries the number of matches it expects, and the build fails if it does not
        /// find exactly that many. These are edits to somebody else's file, made from a distance:
        /// silently patching nothing is the one outcome worth ruling out.
        /// </summary>
        private static readonly (string File, string Pattern, string Replacement, int Count)[] Markup =
        [
            // AcrylicBrush has no BackgroundSource in WinUI 3 - there is one acrylic, and it is the
            // in-app one these four ask for by name.
            (@"App.xaml", @"\s*BackgroundSource=""Backdrop""", "", 4),

            // The EdgeHTML WebView, and the presenter it templates. Both are #if'd out of
            // WebViewer.cs for this flavour; this is the markup half of the same removal.
            (@"Themes\Generic.xaml", @"\s*<Style TargetType=""local:EdgeWebPresenter"">.*?</Style>", "", 1),
        ];

        private static int Main(string[] args)
        {
            if (args.Length > 2 && args[0] == "--markup")
            {
                return PatchMarkup(args[1], args[2]);
            }

            if (args.Length > 3 && args[0] == "--usings")
            {
                return Usings.Generate(args[1], args[2], args.Skip(3));
            }

            Console.Error.WriteLine("Usage: --markup <root> <out> | --usings <root> <out file> <assembly>...");
            return 1;
        }

        /// <summary>
        /// Applies <see cref="Markup"/>, writing each patched file into the output directory.
        /// Incremental, and loud when a rule stops matching: a XAML file that changed underneath a
        /// patch is a build error, not a silent no-op.
        /// </summary>
        private static int PatchMarkup(string root, string output)
        {
            root = WithSeparator(Path.GetFullPath(root));
            output = WithSeparator(Path.GetFullPath(output));

            var version = File.GetLastWriteTimeUtc(typeof(Program).Assembly.Location);
            var written = 0;

            foreach (var group in Markup.GroupBy(x => x.File))
            {
                var input = Path.Combine(root, group.Key);
                var target = Path.Combine(output, group.Key);

                if (!File.Exists(input))
                {
                    Console.Error.WriteLine($"error: {group.Key} was not found; the markup patch names a file that is gone.");
                    return 1;
                }

                if (File.Exists(target))
                {
                    var stamp = File.GetLastWriteTimeUtc(target);
                    if (stamp > File.GetLastWriteTimeUtc(input) && stamp > version)
                    {
                        continue;
                    }
                }

                var text = Encoding.Latin1.GetString(File.ReadAllBytes(input));

                foreach (var (_, pattern, replacement, count) in group)
                {
                    // Singleline so a rule may span lines - the EdgeWebPresenter style is a block.
                    var regex = new Regex(pattern, RegexOptions.Singleline);
                    var found = regex.Matches(text).Count;

                    if (found != count)
                    {
                        Console.Error.WriteLine($"error: {group.Key}: expected {count} match(es) of /{pattern}/, found {found}.");
                        return 1;
                    }

                    text = regex.Replace(text, replacement);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, Encoding.Latin1.GetBytes(text));
                written++;
            }

            Console.WriteLine($"Patched {written} markup file(s) into {output}");
            return 0;
        }

        private static string WithSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
