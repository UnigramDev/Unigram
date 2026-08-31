using System;

namespace Telegram.Generators
{
    /// <summary>
    /// Where the Telegram.Td.Api surface is emitted from, set by the consuming project:
    ///
    ///   &lt;TdSchemaMode&gt;MSBuild&lt;/TdSchemaMode&gt;
    ///   &lt;CompilerVisibleProperty Include="TdSchemaMode" /&gt;
    ///
    /// The text emitted is identical either way; only who compiles it differs.
    /// </summary>
    internal enum TdSchemaMode
    {
        /// <summary>
        /// SchemaGenerator adds the surface to the compilation. Nothing else can see it - a source
        /// generator never observes another generator's output - so CsWinRT's own generator cannot
        /// resolve those types and files their vtables under an unqualified key
        /// ("Telegram.Vector`1[Message]") that type.ToString() never produces at runtime.
        /// </summary>
        Roslyn,

        /// <summary>
        /// An MSBuild target writes the surface to obj/ before CoreCompile and it is compiled as
        /// ordinary source, which is the only arrangement where CsWinRT resolves those types. Go
        /// back to Roslyn once it can resolve them on its own.
        /// </summary>
        MSBuild
    }

    internal static class TdSchemaModeExtensions
    {
        /// <summary>
        /// Anything unrecognized reads as Roslyn rather than failing the build: a typo in a project
        /// file must not silently leave the schema unemitted, which surfaces as thousands of
        /// "type or namespace not found" errors with no hint of the cause.
        /// </summary>
        public static TdSchemaMode Parse(string value)
        {
            return string.Equals(value, "MSBuild", StringComparison.OrdinalIgnoreCase)
                ? TdSchemaMode.MSBuild
                : TdSchemaMode.Roslyn;
        }
    }
}
