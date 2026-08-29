using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Telegram.Generators.Emit;
using Telegram.Generators.Schema;

namespace Telegram.Generators
{
    /// <summary>
    /// Generates the Telegram.Td.Api surface - roughly 3000 classes and their JSON parsers - from
    /// Libraries/tdjson/td_api.tl, the same scheme the vendored tdjson.dll is built from.
    ///
    /// The generated file is not checked in; read it at
    /// obj/&lt;config&gt;/&lt;tfm&gt;/generated/Telegram.Generators/... after a build.
    /// </summary>
    [Generator]
    public class SchemaGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor SchemaFailed = new DiagnosticDescriptor(
            id: "TDAPI001",
            title: "td_api.tl could not be read",
            messageFormat: "td_api.tl line {0}: {1}",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GeneratorCrashed = new DiagnosticDescriptor(
            id: "TDAPI002",
            title: "The API generator threw",
            messageFormat: "{0}",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var content = context.AdditionalTextsProvider
                .Where(f => f.Path.EndsWith("td_api.tl"))
                .Select((file, _) => file.GetText()?.ToString());

            //   <TdParsers>Pointer</TdParsers>
            //   <CompilerVisibleProperty Include="TdParsers" />
            //
            // Which set of parsers to emit - see TdParsers. Unset means Reader, which is what the
            // app shipped for years and what the byte-for-byte diff is taken against.
            var parsers = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
                options.GlobalOptions.TryGetValue("build_property.TdParsers", out var value)
                    ? TdParsersExtensions.Parse(value)
                    : TdParsers.Reader);

            //   <TdSchemaMode>MSBuild</TdSchemaMode>
            //   <CompilerVisibleProperty Include="TdSchemaMode" />
            //
            // Who compiles the surface - see TdSchemaMode. Unset means Roslyn, which is this
            // generator emitting it.
            var mode = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
                options.GlobalOptions.TryGetValue("build_property.TdSchemaMode", out var value)
                    ? TdSchemaModeExtensions.Parse(value)
                    : TdSchemaMode.Roslyn);

            context.RegisterSourceOutput(content.Combine(parsers).Combine(mode),
                (ctx, pair) => Execute(ctx, pair.Left.Left, pair.Left.Right, pair.Right));
        }

        private static void Execute(SourceProductionContext context, string text, TdParsers parsers, TdSchemaMode mode)
        {
            // Telegram.Generators.Cli has already written the same text into obj/ and MSBuild has
            // added it to @(Compile); emitting here as well would declare every type twice.
            if (text is null || mode == TdSchemaMode.MSBuild)
            {
                return;
            }

            try
            {
                var schema = TlParser.Parse(text);

                // Reported rather than thrown: a generator that produces nothing turns one bad line
                // into thousands of "type or namespace not found" errors with no hint of the cause.
                foreach (var error in schema.Errors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SchemaFailed, Location.None, error.Line, error.Message));
                }

                context.AddSource("TdDotNetApi.g.cs", Write(schema.Classes, parsers));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(GeneratorCrashed, Location.None, ex.ToString()));
            }
        }

        internal static string Write(List<SchemaClass> parsed, TdParsers parsers)
        {
            var reader = parsers != TdParsers.Pointer;
            var pointer = parsers != TdParsers.Reader;

            var classes = parsed
                .OrderBy(x => x.IsFunction)
                .ThenBy(x => x.Name)
                .ToList();

            // Types a function can return, i.e. everything that can arrive as a response rather
            // than only nested inside one. These are what the top-level dispatcher switches over.
            var functionTypes = new HashSet<string>(classes.Where(x => x.IsFunction).Select(x => x.ReturnType));

            var abstractTypes = classes
                .Where(x => !x.IsFunction && x.ReturnType != null)
                .GroupBy(x => Naming.ToPascalCase(x.ReturnType))
                .ToDictionary(x => x.Key, y => y.ToList());

            // Grouped before flattening, so constructors of the same base stay together in the
            // switch. The order is load-bearing only in that it must stay stable build to build.
            var returnTypes = classes
                .Where(x => !x.IsFunction)
                .GroupBy(x => Naming.ToPascalCase(x.ReturnType ?? x.Name))
                .Where(x => functionTypes.Contains(x.Key))
                .SelectMany(x => x)
                .Where(x => !x.IsProxy)
                .ToList();

            // Everything reachable from a request, which is the set that needs a ToJson.
            var serializable = FindReachable(classes, classes.Where(x => x.IsFunction));

            // TODO: the mirror of this - emitting FromJson only for types that can actually be
            // received - used to exist as `toJsonTypes.Except(referencedTypes)` and had to be
            // removed because it stopped being correct. It would cut a large slice of the ~150k
            // generated lines, which is worth real .NET Native compile time and binary size.
            //
            // What makes it hard is that "received" is not the same as "reachable from a function
            // return type". Updates arrive unsolicited and only qualify because the scheme happens
            // to declare `testUseUpdate = Update;`; richMessageSourceBlocks does not qualify at all
            // and is patched in by hand below. Any type in that position is silently given no
            // parser, and the failure shows up at runtime as a null rather than at build time.
            // Restoring this needs a definition of "received" that does not depend on the scheme
            // incidentally naming a type in a function signature.

            var builder = new StringBuilder();

            // Roslyn exempts a file from analyzers when its name matches a generated pattern or it
            // opens with this comment. The name carries it while this is generator output; the
            // header is what keeps the exemption if the file is ever checked in and compiled as
            // ordinary source, which is the only way CsWinRT's own generator can resolve these
            // types - generators never see each other's output.
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("//     This code was generated from td_api.tl by Telegram.Generators.SchemaGenerator.");
            builder.AppendLine("//");
            builder.AppendLine("//     Changes to this file will be lost if the code is regenerated.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine("//------------------------------------------------------------------------------");
            builder.AppendLine();

            if (pointer)
            {
                // MemoryExtensions.SequenceEqual, which the pointer parsers compare field names
                // with. Added only in this mode so the default output stays byte for byte what it
                // has always been.
                builder.AppendLine("using System;");
            }

            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Text.Json;");
            builder.AppendLine();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("#pragma warning disable CS8601 // Possible null reference assignment.");
            builder.AppendLine("#pragma warning disable CS8603 // Possible null reference return.");
            builder.AppendLine("#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.");
            builder.AppendLine();
            builder.AppendLine("namespace Telegram.Td.Api");
            builder.AppendLine("{");

            foreach (var type in classes)
            {
                ApiEmitter.WriteClass(builder, type, serializable.Contains(type));
            }

            builder.AppendLine();

            builder.AppendLine("public partial class ClientJson");
            builder.AppendLine("{");

            // richMessageSourceBlocks is reachable only through richMessageSource, which no function
            // returns - but the instant view code parses it from a top-level payload, so it needs a
            // case here that the reachability rules above would not give it.
            var richMessageSourceBlocks = classes.FirstOrDefault(x => x.Name == "richMessageSourceBlocks");
            if (richMessageSourceBlocks != null)
            {
                returnTypes.Add(richMessageSourceBlocks);
            }

            if (reader)
            {
                WriteReaderParsers(builder, classes, returnTypes, abstractTypes);
            }

            if (pointer)
            {
                WritePointerParsers(builder, classes, returnTypes, abstractTypes);
            }

            builder.AppendLine("}");
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// The Utf8JsonReader parsers, which is what the app shipped for years. Byte for byte what
        /// this generator has always emitted, and the diff that proves a change to it is safe.
        /// </summary>
        private static void WriteReaderParsers(StringBuilder builder, List<SchemaClass> classes,
            List<SchemaClass> returnTypes, Dictionary<string, List<SchemaClass>> abstractTypes)
        {
            builder.AppendLine("  private static Object DoFromJson(ref Utf8JsonReader reader, ClientResultHandler handler, uint hash)");
            builder.AppendLine("  {");
            builder.AppendLine("    switch (hash)");
            builder.AppendLine("    {");

            foreach (var type in returnTypes)
            {
                ApiEmitter.WriteDispatchCase(builder, type);
            }

            builder.AppendLine("        default: return null;");
            builder.AppendLine("    }");
            builder.AppendLine("  }");

            foreach (var type in classes)
            {
                if (type.IsFunction)
                {
                    continue;
                }

                builder.AppendLine();

                // An abstract type gets a dispatcher over its constructors; a concrete one gets a
                // parser. Keyed on the raw name because that is what //@class declares.
                if (abstractTypes.TryGetValue(type.Name, out var constructors))
                {
                    ApiEmitter.WriteAbstractDispatcher(builder, type.Name, constructors);
                }
                else
                {
                    ApiEmitter.WriteFromJson(builder, type);
                }
            }
        }

        /// <summary>
        /// The same parsers reading through TdJsonReader instead of Utf8JsonReader. Same shape,
        /// same schema, different reader. Emitted alongside the reader set only for the benchmark,
        /// which races them; the app compiles one or the other.
        /// </summary>
        private static void WritePointerParsers(StringBuilder builder, List<SchemaClass> classes,
            List<SchemaClass> returnTypes, Dictionary<string, List<SchemaClass>> abstractTypes)
        {
            builder.AppendLine();
            // The entry point lives here rather than beside the other pointer helpers so that
            // PtrClientJson.cs refers to nothing generated, and can therefore be compiled by a
            // project that has this mode switched off - which the app does until it switches over.
            builder.AppendLine("  /// <summary>Entry point: the whole payload, straight off td_receive's pointer.</summary>");
            builder.AppendLine("  public static unsafe Object FromPtr(byte* buffer, int length, ClientResultHandler? handler = null)");
            builder.AppendLine("  {");
            builder.AppendLine("    var reader = new TdJsonReader(buffer, length);");
            builder.AppendLine("    reader.Read();");
            builder.AppendLine();
            builder.AppendLine("    if (reader.TokenType == JsonTokenType.StartObject)");
            builder.AppendLine("    {");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine();
            builder.AppendLine("        if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(\"@type\"u8))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            var hash = reader.ValueCrc32();");
            builder.AppendLine();
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return DoFromPtr(ref reader, handler, hash);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    return new Error(400, \"Can't deserialize\");");
            builder.AppendLine("  }");
            builder.AppendLine();
            builder.AppendLine("  private static Object DoFromPtr(ref TdJsonReader reader, ClientResultHandler handler, uint hash)");
            builder.AppendLine("  {");
            builder.AppendLine("    switch (hash)");
            builder.AppendLine("    {");

            foreach (var type in returnTypes)
            {
                ApiEmitter.WritePtrDispatchCase(builder, type);
            }

            builder.AppendLine("        default: return null;");
            builder.AppendLine("    }");
            builder.AppendLine("  }");

            foreach (var type in classes)
            {
                if (type.IsFunction)
                {
                    continue;
                }

                builder.AppendLine();

                if (abstractTypes.TryGetValue(type.Name, out var constructors))
                {
                    ApiEmitter.WritePtrAbstractDispatcher(builder, type.Name, constructors);
                }
                else
                {
                    ApiEmitter.WriteFromPtr(builder, type);
                }
            }
        }

        /// <summary>
        /// Every type reachable from the given roots by following field types through abstract
        /// bases. Ordered, because the caller's output order has to be stable across builds.
        /// </summary>
        private static HashSet<SchemaClass> FindReachable(List<SchemaClass> classes, IEnumerable<SchemaClass> roots)
        {
            var byBaseType = classes
                .Where(x => !x.IsFunction)
                .GroupBy(x => Naming.ToPascalCase(x.ReturnType ?? x.Name))
                .ToDictionary(x => x.Key, y => y.ToList());

            var seen = new HashSet<SchemaClass>();

            void Visit(SchemaClass type)
            {
                if (!seen.Add(type))
                {
                    return;
                }

                foreach (var property in type.Properties)
                {
                    if (byBaseType.TryGetValue(Naming.ToPascalCase(property.Type), out var constructors))
                    {
                        constructors.ForEach(Visit);
                    }
                }
            }

            foreach (var root in roots)
            {
                Visit(root);
            }

            return seen;
        }
    }
}
