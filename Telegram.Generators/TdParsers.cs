using System;

namespace Telegram.Generators
{
    /// <summary>
    /// Which set of FromJson parsers SchemaGenerator emits, set by the consuming project:
    ///
    ///   &lt;TdParsers&gt;Pointer&lt;/TdParsers&gt;
    ///   &lt;CompilerVisibleProperty Include="TdParsers" /&gt;
    ///
    /// The app builds one or the other, never both - two parsers for the same schema is ~44k lines
    /// of .NET Native compile time for a set nothing calls. Telegram.Benchmarks is the exception
    /// and builds Both, because its whole argument is the two of them racing over one corpus and
    /// agreeing field for field.
    /// </summary>
    internal enum TdParsers
    {
        /// <summary>Utf8JsonReader only. The default, and what the app shipped for years.</summary>
        Reader,

        /// <summary>TdJsonReader only, reading td_receive's buffer where it lies.</summary>
        Pointer,

        /// <summary>Both, for comparing them.</summary>
        Both
    }

    internal static class TdParsersExtensions
    {
        /// <summary>
        /// Anything unrecognized reads as Reader rather than failing the build: a typo in a project
        /// file should not silently emit the other parser, and Reader is the one that shipped.
        /// </summary>
        public static TdParsers Parse(string value)
        {
            if (string.Equals(value, "Pointer", StringComparison.OrdinalIgnoreCase))
            {
                return TdParsers.Pointer;
            }
            else if (string.Equals(value, "Both", StringComparison.OrdinalIgnoreCase))
            {
                return TdParsers.Both;
            }

            return TdParsers.Reader;
        }
    }
}
