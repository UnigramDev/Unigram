using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Telegram.Generators
{
    /// <summary>
    /// Generates the localization surface from Strings/en/Resources.xml: <c>Strings</c>, which is
    /// R plus a property per key reading through ILocaleService, and <c>LocaleFallback</c>, which
    /// is the English text those keys resolve to when the cloud language pack has no answer.
    ///
    /// It replaces UnigramUtils' SynchronizeResources, which lived outside the repository only
    /// because it also had to write Resources.resw and a source generator cannot emit one. The
    /// English text now comes from here instead of the .resw, so nothing is left to generate
    /// there and the generator can come in.
    ///
    /// The generated files are not checked in; read them at
    /// obj/&lt;config&gt;/&lt;tfm&gt;/generated/Telegram.Generators/... after a build.
    /// </summary>
    [Generator]
    public class ResourcesGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor GeneratorCrashed = new DiagnosticDescriptor(
            id: "TDLOC001",
            title: "The resources generator threw",
            messageFormat: "{0}",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Android's plural forms, which the XML carries as separate suffixed entries. A key
        /// wearing one of these gets an R constant instead of a property: it is read through
        /// Locale.Declension, which picks the form from the current language's rules.
        /// </summary>
        private static readonly string[] PluralSuffixes = { "_zero", "_one", "_two", "_few", "_many", "_other" };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var content = context.AdditionalTextsProvider
                .Where(f => f.Path.EndsWith("Resources.xml"))
                .Select((file, _) => file.GetText()?.ToString());

            context.RegisterSourceOutput(content, Execute);
        }

        private static void Execute(SourceProductionContext context, string text)
        {
            if (text is null)
            {
                return;
            }

            try
            {
                var nodes = Parse(text);

                context.AddSource("Strings.g.cs", WriteStrings(nodes));
                context.AddSource("LocaleFallback.g.cs", WriteFallback(nodes));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(GeneratorCrashed, Location.None, ex.ToString()));
            }
        }

        private static List<KeyValuePair<string, string>> Parse(string text)
        {
            var nodes = new Dictionary<string, string>(StringComparer.Ordinal);
            var document = XDocument.Parse(text);

            foreach (var item in document.Root.Descendants("string"))
            {
                var name = item.Attribute("name");

                // An empty entry is a key the translators have not filled in. It would emit a
                // property returning "" and hide the miss, so it never reaches either file.
                if (name != null && item.Value.Length > 0)
                {
                    nodes[name.Value] = item.Value;
                }
            }

            return nodes.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        }

        private static bool IsPlural(string key, out string singular)
        {
            foreach (var suffix in PluralSuffixes)
            {
                if (key.EndsWith(suffix, StringComparison.Ordinal))
                {
                    singular = key.Substring(0, key.Length - suffix.Length);
                    return true;
                }
            }

            singular = null;
            return false;
        }

        private static string WriteStrings(List<KeyValuePair<string, string>> nodes)
        {
            var builder = new StringBuilder(1024 * 1024);

            WriteHeader(builder);

            builder.AppendLine("namespace Telegram");
            builder.AppendLine("{");
            builder.AppendLine("    using Telegram.Services;");
            builder.AppendLine();
            builder.AppendLine("    public sealed partial class Strings");
            builder.AppendLine("    {");
            builder.AppendLine("        private static readonly ILocaleService Resource;");
            builder.AppendLine();
            builder.AppendLine("        static Strings()");
            builder.AppendLine("        {");
            builder.AppendLine("            Resource = LocaleService.Current;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        /// <summary>The pluralized keys, to be read through Locale.Declension.</summary>");
            builder.AppendLine("        public static class R");
            builder.AppendLine("        {");

            var plurals = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in nodes)
            {
                if (IsPlural(entry.Key, out var singular) && plurals.Add(singular))
                {
                    builder.AppendLine("            public const string " + singular + " = \"" + singular + "\";");
                }
            }

            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var entry in nodes)
            {
                if (IsPlural(entry.Key, out _))
                {
                    continue;
                }

                builder.AppendLine("        /// <summary>");
                builder.AppendLine("        /// Localized resource similar to \"" + Documentation(entry.Value) + "\"");
                builder.AppendLine("        /// </summary>");
                builder.AppendLine("        public static string " + entry.Key + " => Resource.GetString(\"" + entry.Key + "\");");
                builder.AppendLine();
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            return builder.ToString();
        }

        private static string WriteFallback(List<KeyValuePair<string, string>> nodes)
        {
            // Bucketed by key length so no single method carries all 7000 arms: the legacy project
            // still compiles this through .NET Native, and one method that size is a bad bet there.
            // Length is the one discriminator available before the lookup itself, and it costs an
            // int jump table.
            var buckets = new SortedDictionary<int, List<KeyValuePair<string, string>>>();

            foreach (var entry in nodes)
            {
                if (!buckets.TryGetValue(entry.Key.Length, out var bucket))
                {
                    bucket = buckets[entry.Key.Length] = new List<KeyValuePair<string, string>>();
                }

                bucket.Add(entry);
            }

            var builder = new StringBuilder(1024 * 1024);

            WriteHeader(builder);

            builder.AppendLine("namespace Telegram");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// The English text behind every key, for the ones the cloud language pack does not");
            builder.AppendLine("    /// answer for. Read by LocaleService, and by nothing else.");
            builder.AppendLine("    ///");
            builder.AppendLine("    /// <para>A switch rather than a dictionary: the table is cold - a hit means the pack was");
            builder.AppendLine("    /// missing a string - and a switch costs nothing until it is called, where a dictionary");
            builder.AppendLine("    /// would hash every key into the heap at startup and stay there.</para>");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine("    internal static class LocaleFallback");
            builder.AppendLine("    {");
            builder.AppendLine("        /// <summary>The English text for <paramref name=\"key\"/>, or null if there is none.</summary>");
            builder.AppendLine("        public static string GetString(string key)");
            builder.AppendLine("        {");
            builder.AppendLine("            return key.Length switch");
            builder.AppendLine("            {");

            foreach (var bucket in buckets)
            {
                builder.AppendLine("                " + bucket.Key + " => Length" + bucket.Key + "(key),");
            }

            builder.AppendLine("                _ => null");
            builder.AppendLine("            };");
            builder.AppendLine("        }");

            foreach (var bucket in buckets)
            {
                builder.AppendLine();
                builder.AppendLine("        private static string Length" + bucket.Key + "(string key)");
                builder.AppendLine("        {");
                builder.AppendLine("            return key switch");
                builder.AppendLine("            {");

                foreach (var entry in bucket.Value)
                {
                    builder.AppendLine("                \"" + entry.Key + "\" => \"" + Literal(entry.Value) + "\",");
                }

                builder.AppendLine("                _ => null");
                builder.AppendLine("            };");
                builder.AppendLine("        }");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            return builder.ToString();
        }

        private static void WriteHeader(StringBuilder builder)
        {
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("//     This code was generated from Resources.xml by Telegram.Generators.ResourcesGenerator.");
            builder.AppendLine("//");
            builder.AppendLine("//     Changes to this file will be lost if the code is regenerated.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine();
        }

        /// <summary>
        /// Android's own escaping, which the XML carries exactly as it was exported.
        /// </summary>
        private static string Unescape(string value)
        {
            return value
                .Replace("\\\"", "\"")
                .Replace("\\'", "'");
        }

        /// <summary>
        /// The value as a C# string literal. The \n the XML carries becomes a line feed, and a
        /// line feed alone: the .resw wrote CRLF, but XML normalizes that back to LF on the way
        /// in, so LF is what the app has been handed all along.
        /// </summary>
        private static string Literal(string value)
        {
            var text = Unescape(value.Replace("\\n", "\n"));
            var builder = new StringBuilder(text.Length + 16);

            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// The value as the body of a &lt;summary&gt;, which is XML: the handful of strings
        /// carrying &amp; or &lt; would otherwise emit a doc comment that does not parse.
        /// </summary>
        private static string Documentation(string value)
        {
            return Unescape(value)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\\n", "\r\n        /// ");
        }
    }
}
