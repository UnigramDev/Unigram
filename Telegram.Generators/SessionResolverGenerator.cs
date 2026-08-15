using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Telegram.Generators
{
    /// <summary>
    /// Generates SessionImpl's dependency container - the singleton fields, the constructor that
    /// builds them in dependency order, and the Resolve&lt;T&gt; switch - from the registrations
    /// listed on [GenerateResolver].
    ///
    /// It replaces TypeContainerGenerator, which did the same job by reflecting over the app's own
    /// types from a [Conditional("DEBUG")] method run by hand, whose output was pasted into
    /// Services/Session.g.cs. Those three calls - GetTypes, GetConstructors, GetProperties - were
    /// the whole of NativeAOT's trim warnings once TypeCrosserGenerator was deleted. They were also
    /// the reason the file drifted: by the time it was replaced the generator no longer described
    /// its own output, disagreeing about the class name and five of the registrations.
    ///
    /// The generated file is not checked in; read it at
    /// obj/&lt;config&gt;/&lt;tfm&gt;/generated/Telegram.Generators/... after a build.
    /// </summary>
    [Generator]
    public class SessionResolverGenerator : IIncrementalGenerator
    {
        private const string AttributeName = "Telegram.Services.GenerateResolverAttribute";

        private static readonly DiagnosticDescriptor NoConstructor = new DiagnosticDescriptor(
            id: "TDDI001",
            title: "A registered type has no usable constructor",
            messageFormat: "'{0}' is registered on [GenerateResolver] but has no public constructor",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor CircularDependency = new DiagnosticDescriptor(
            id: "TDDI003",
            title: "Registered singletons depend on each other in a cycle",
            messageFormat: "No construction order exists for: {0}",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GeneratorCrashed = new DiagnosticDescriptor(
            id: "TDDI002",
            title: "The resolver generator threw",
            messageFormat: "{0}",
            category: "Telegram.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // Emitted rather than written by hand, so the app carries no file whose only purpose is to
        // let the generator read it back.
        private const string AttributeSource = @"//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Services
{
    /// <summary>Registrations for the generated Resolve&lt;T&gt;. See SessionResolverGenerator.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateResolverAttribute : Attribute
    {
        /// <summary>The interfaces the session itself satisfies. Resolving any of them returns this.</summary>
        public Type[] Self { get; set; }

        /// <summary>Passed in to the constructor and stored; owned by the lifetime, not the session.</summary>
        public Type[] Globals { get; set; }

        /// <summary>The globals that Resolve also hands out. The rest are constructor-only.</summary>
        public Type[] Exposed { get; set; }

        /// <summary>Interface, implementation, interface, implementation. Built eagerly, in dependency order.</summary>
        public Type[] Singletons { get; set; }

        /// <summary>Same pairing, but built on first Resolve.</summary>
        public Type[] Lazy { get; set; }

        /// <summary>A new one per Resolve.</summary>
        public Type[] Instances { get; set; }
    }
}
";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(
                ctx => ctx.AddSource("GenerateResolverAttribute.g.cs", AttributeSource));

            var target = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (_, _) => true,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);

            context.RegisterSourceOutput(target, static (ctx, symbol) =>
            {
                try
                {
                    Execute(ctx, symbol);
                }
                catch (Exception ex)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(GeneratorCrashed, Location.None, ex.ToString()));
                }
            });
        }

        private static void Execute(SourceProductionContext context, INamedTypeSymbol session)
        {
            var attribute = session.GetAttributes()
                .First(x => x.AttributeClass?.ToDisplayString() == AttributeName);

            var self = ReadTypes(attribute, "Self");
            var exposed = ReadTypes(attribute, "Exposed");
            var globals = ReadTypes(attribute, "Globals");
            var singletons = ReadPairs(attribute, "Singletons");
            var lazy = ReadPairs(attribute, "Lazy");
            var instances = ReadTypes(attribute, "Instances");

            // A singleton cannot depend on something built later, so any lazy registration one of
            // them takes has to be promoted to eager and moved ahead of it. TypeContainerGenerator
            // did this too; it is what keeps the constructor body a straight line of assignments
            // rather than a graph.
            for (int i = 0; i < singletons.Count; i++)
            {
                foreach (var parameter in Parameters(singletons[i].Value))
                {
                    var promoted = lazy.FirstOrDefault(x => Same(x.Key, parameter.Type));
                    if (promoted.Key != null)
                    {
                        lazy.Remove(promoted);
                        singletons.Insert(0, promoted);
                        i++;
                    }
                }
            }

            singletons = Order(singletons, context);

            var model = new Model(self, exposed, globals, singletons, lazy, instances, context);
            context.AddSource("Session.Resolver.g.cs", model.Emit(session));
        }

        private sealed class Model
        {
            private readonly List<INamedTypeSymbol> _self;
            private readonly List<INamedTypeSymbol> _exposed;
            private readonly List<INamedTypeSymbol> _globals;
            private readonly List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> _singletons;
            private readonly List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> _lazy;
            private readonly List<INamedTypeSymbol> _instances;
            private readonly SourceProductionContext _context;

            public Model(List<INamedTypeSymbol> self,
                List<INamedTypeSymbol> exposed,
                List<INamedTypeSymbol> globals,
                List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> singletons,
                List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> lazy,
                List<INamedTypeSymbol> instances,
                SourceProductionContext context)
            {
                _self = self;
                _exposed = exposed;
                _globals = globals;
                _singletons = singletons;
                _lazy = lazy;
                _instances = instances;
                _context = context;
            }

            public string Emit(INamedTypeSymbol session)
            {
                var builder = new StringBuilder();

                builder.AppendLine("//");
                builder.AppendLine("// Copyright (c) Fela Ameghino 2015-2026");
                builder.AppendLine("//");
                builder.AppendLine("// Distributed under the GNU General Public License v3.0. (See accompanying");
                builder.AppendLine("// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)");
                builder.AppendLine("//");
                builder.AppendLine();
                builder.AppendLine("namespace " + session.ContainingNamespace.ToDisplayString());
                builder.AppendLine("{");
                builder.AppendLine("    public partial class " + session.Name);
                builder.AppendLine("    {");
                builder.AppendLine("        private readonly int _id;");
                builder.AppendLine();

                foreach (var global in _globals)
                {
                    builder.AppendLine("        private readonly " + Full(global) + " " + Field(global) + ";");
                }

                builder.AppendLine();

                foreach (var singleton in _singletons)
                {
                    builder.AppendLine("        private readonly " + Full(singleton.Key) + " " + Field(singleton.Key) + ";");
                }

                // Not a singleton, because it is this. Named _sessionService rather than after its
                // type because that is what every constructor taking one is written against.
                builder.AppendLine("        private readonly " + Full(_self[0]) + " _sessionService;");

                builder.AppendLine();

                foreach (var single in _lazy)
                {
                    builder.AppendLine("        private " + Full(single.Key) + " " + Field(single.Key) + ";");
                }

                builder.AppendLine();

                builder.Append("        public " + session.Name + "(");
                foreach (var global in _globals)
                {
                    builder.Append(Full(global) + " " + Field(global, false) + ", ");
                }
                builder.AppendLine("int session, bool active)");
                builder.AppendLine("        {");
                builder.AppendLine("            _id = session;");
                builder.AppendLine("            _sessionService = this;");
                builder.AppendLine();

                foreach (var global in _globals)
                {
                    builder.AppendLine("            " + Field(global) + " = " + Field(global, false) + ";");
                }

                builder.AppendLine();

                // Two interfaces can share one implementation; the second must alias the first
                // rather than construct a second copy of it.
                var built = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);

                foreach (var singleton in _singletons)
                {
                    if (built.TryGetValue(singleton.Value, out var already))
                    {
                        builder.AppendLine("            " + Field(singleton.Key) + " = " + Field(already) + ";");
                    }
                    else
                    {
                        builder.AppendLine("            " + Field(singleton.Key) + " = " + Construct(singleton.Value, 3) + ";");
                    }

                    built[singleton.Value] = singleton.Key;
                }

                builder.AppendLine();
                builder.AppendLine("            Initialize(active);");
                builder.AppendLine("        }");
                builder.AppendLine();

                builder.AppendLine("        public T Resolve<T>()");
                builder.AppendLine("        {");
                builder.AppendLine("            switch (typeof(T).FullName)");
                builder.AppendLine("            {");

                foreach (var instance in _instances)
                {
                    builder.AppendLine("                case \"" + Full(instance, false) + "\":");
                    builder.AppendLine("                    return (T)(object)" + Construct(instance, 5) + ";");
                }

                foreach (var singleton in _singletons)
                {
                    builder.AppendLine("                case \"" + Full(singleton.Key, false) + "\":");
                    builder.AppendLine("                    return (T)" + Field(singleton.Key) + ";");
                }

                foreach (var single in _lazy)
                {
                    builder.AppendLine("                case \"" + Full(single.Key, false) + "\":");
                    builder.AppendLine("                    return (T)(" + Field(single.Key) + " ??= " + Construct(single.Value, 5) + ");");
                }

                foreach (var exposed in _exposed)
                {
                    builder.AppendLine("                case \"" + Full(exposed, false) + "\":");
                    builder.AppendLine("                    return (T)" + Field(exposed) + ";");
                }

                foreach (var self in _self)
                {
                    builder.AppendLine("                case \"" + Full(self, false) + "\":");
                    builder.AppendLine("                    return (T)(object)this;");
                }
                builder.AppendLine("                default:");
                builder.AppendLine("                    return default;");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");

                return builder.ToString();
            }

            private string Construct(INamedTypeSymbol type, int depth)
            {
                // A lazy registration named as a dependency means the implementation, not the
                // interface: there is nothing to new up otherwise.
                var promoted = _lazy.FirstOrDefault(x => Same(x.Key, type));
                if (promoted.Key != null)
                {
                    type = promoted.Value;
                }

                var parameters = Parameters(type);
                if (parameters.Length == 0)
                {
                    if (!type.Constructors.Any(x => x.DeclaredAccessibility == Accessibility.Public))
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(NoConstructor, Location.None, Full(type, false)));
                    }

                    return "new " + Full(type) + "()";
                }

                var arguments = parameters.Select(x => Argument(x, depth)).ToArray();
                if (arguments.Length == 1)
                {
                    return "new " + Full(type) + "(" + arguments[0] + ")";
                }

                // "\r\n" rather than Environment.NewLine: generated output must not depend on the
                // host, and RS1035 bans the latter in analyzers for exactly that reason.
                var indent = new string(' ', (depth + 1) * 4);
                return "new " + Full(type) + "(\r\n"
                    + indent + string.Join(",\r\n" + indent, arguments) + ")";
            }

            private string Argument(IParameterSymbol parameter, int depth)
            {
                if (_globals.Any(x => Same(x, parameter.Type)) || _singletons.Any(x => Same(x.Key, parameter.Type)))
                {
                    return Field(parameter.Type);
                }

                var promoted = _lazy.FirstOrDefault(x => Same(x.Key, parameter.Type));
                if (promoted.Key != null)
                {
                    return Field(parameter.Type) + " ??= " + Construct(promoted.Value, depth + 1);
                }

                if (_self.Any(x => Same(x, parameter.Type)))
                {
                    return "_sessionService";
                }

                // The session id and the initial online flag are the constructor's own arguments,
                // matched by name because nothing about their types distinguishes them.
                if (parameter.Name == "session")
                {
                    return "_id";
                }

                if (parameter.Name == "active" || parameter.Name == "online")
                {
                    return "active";
                }

                return "Resolve<" + Full(parameter.Type) + ">()";
            }
        }

        /// <summary>
        /// Orders the singletons so that each is constructed after everything it takes. A real
        /// topological sort, unlike the comparison this replaces - "1 if x depends on y, otherwise
        /// -1" is not a consistent ordering, so List.Sort was entitled to return anything and the
        /// result merely happened to be valid. It matters because these are fields: constructing
        /// one before its dependency compiles perfectly well and leaves a null behind.
        ///
        /// Independent entries keep their registration order, so the output is stable.
        /// </summary>
        private static List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> Order(
            List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> singletons,
            SourceProductionContext context)
        {
            var ordered = new List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>>(singletons.Count);
            var pending = new List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>>(singletons);

            while (pending.Count > 0)
            {
                var index = pending.FindIndex(candidate => !Parameters(candidate.Value).Any(
                    parameter => pending.Any(other =>
                        !Same(other.Key, candidate.Key) && Same(other.Key, parameter.Type))));

                if (index < 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(CircularDependency, Location.None,
                        string.Join(", ", pending.Select(x => x.Key.Name))));

                    ordered.AddRange(pending);
                    break;
                }

                ordered.Add(pending[index]);
                pending.RemoveAt(index);
            }

            return ordered;
        }

        private static IParameterSymbol[] Parameters(INamedTypeSymbol type)
        {
            var constructor = type.Constructors
                .FirstOrDefault(x => x.DeclaredAccessibility == Accessibility.Public && !x.IsStatic);

            return constructor?.Parameters.Where(x => !x.HasExplicitDefaultValue).ToArray()
                ?? Array.Empty<IParameterSymbol>();
        }

        private static List<INamedTypeSymbol> ReadTypes(AttributeData attribute, string name)
        {
            var value = attribute.NamedArguments.FirstOrDefault(x => x.Key == name).Value;
            if (value.Kind != TypedConstantKind.Array || value.Values.IsDefaultOrEmpty)
            {
                return new List<INamedTypeSymbol>();
            }

            return value.Values
                .Select(x => x.Value as INamedTypeSymbol)
                .Where(x => x != null)
                .ToList();
        }

        private static List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>> ReadPairs(AttributeData attribute, string name)
        {
            var flat = ReadTypes(attribute, name);
            var pairs = new List<KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>>();

            for (int i = 0; i + 1 < flat.Count; i += 2)
            {
                pairs.Add(new KeyValuePair<INamedTypeSymbol, INamedTypeSymbol>(flat[i], flat[i + 1]));
            }

            return pairs;
        }

        private static bool Same(ITypeSymbol x, ITypeSymbol y)
        {
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        private static string Full(ITypeSymbol type, bool global = true)
        {
            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return global ? name : name.Replace("global::", string.Empty);
        }

        // ISomething becomes _something; Something becomes _something.
        private static string Field(ITypeSymbol type, bool underscore = true)
        {
            var name = type.Name;
            if (type.TypeKind == TypeKind.Interface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            {
                name = name.Substring(1);
            }

            return (underscore ? "_" : string.Empty) + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
