using System;
using System.Linq;
using Telegram.Generators.Schema;

namespace Telegram.Generators
{
    /// <summary>
    /// The schema emitter reached from outside the analyzer host, so that Telegram.Generators.Cli
    /// produces byte for byte what SchemaGenerator produces. See <see cref="TdSchemaMode"/>.
    /// </summary>
    public static class SchemaWriter
    {
        /// <param name="parsers">Reader, Pointer or Both; anything else reads as Reader.</param>
        /// <exception cref="InvalidOperationException">The scheme did not parse.</exception>
        public static string Write(string schema, string parsers)
        {
            var parsed = TlParser.Parse(schema);

            // Thrown rather than reported: the Roslyn path can emit a partial file alongside a
            // diagnostic, but a build target that writes one leaves the compiler to report the
            // damage as thousands of missing types instead.
            if (parsed.Errors.Count > 0)
            {
                // "\n" rather than Environment.NewLine: RS1035 bans it in an analyzer assembly,
                // and this file is compiled into one.
                throw new InvalidOperationException(string.Join("\n",
                    parsed.Errors.Select(x => "td_api.tl(" + x.Line + "): " + x.Message)));
            }

            return SchemaGenerator.Write(parsed.Classes, TdParsersExtensions.Parse(parsers));
        }
    }
}
