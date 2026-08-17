//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Telegram.Generators
{
    /// <summary>
    /// Flags a managed collection boxed into a WinRT object - ItemsSource, Tag, Content, or an
    /// element of a marshalled collection - when nothing will have generated a CCW vtable for its
    /// exact type.
    /// </summary>
    /// <remarks>
    /// The dividing line is whether the compiler can see the conversion. A parameter typed
    /// IEnumerable&lt;T&gt; or IVector&lt;T&gt; is a conversion in source, so CsWinRT generates the
    /// marshaller for that instantiation and any concrete type reaches it - which is why
    /// MessageSelector could hand ConfigurePositionXInertiaModifiers an array and see it work. A
    /// parameter typed object is not: the runtime has only the concrete type to go on, and it finds
    /// a vtable for that type only if one was generated. CsWinRT generates them for non-generic
    /// types declared in this assembly, and for nothing else - arrays and constructed generics like
    /// List&lt;T&gt; or ObservableCollection&lt;T&gt; need naming in a
    /// GeneratedWinRTExposedExternalType attribute.
    ///
    /// Elements are the same conversion one level down. GetAt boxes each one on demand, at a point
    /// no call site corresponds to, which is how a List&lt;IList&lt;Rect&gt;&gt; marshals fine and
    /// then throws when the native side reads it.
    ///
    /// The failure is invisible until the feature runs, and rarely looks like a marshalling problem:
    /// set_ItemsSource returns E_INVALIDARG, which on a DispatcherQueue callback fail-fasts the
    /// process rather than throwing. Hence an analyzer: this is the one class of porting error the
    /// compiler could catch and does not.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class WinRTExposedTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TG1001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Collection crossing the WinRT ABI has no CCW vtable",
            "'{0}' is passed to WinRT as '{1}' but has no CCW vtable: add [assembly: GeneratedWinRTExposedExternalType(typeof({0}))] to CsWinRT.cs",
            "Interoperability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Arrays and constructed generic types need a vtable generated for the exact instantiation before WinRT can QI them for IVector or IBindableIterable. Without one the call fails at runtime - as E_INVALIDARG, as an InvalidCastException, or as a fail-fast.");

        public const string ReferenceArrayDiagnosticId = "TG1002";

        private static readonly DiagnosticDescriptor ReferenceArrayRule = new DiagnosticDescriptor(
            ReferenceArrayDiagnosticId,
            "Array of a WinRT struct cannot cross the ABI",
            "'{0}[]' cannot be marshalled as '{1}': '{0}' is a struct, so the array boxes through IReferenceArray. Pass a List<{0}> instead",
            "Interoperability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "An array of a reference type marshals as an array of pointers, but an array of a value type that is not a WinRT fundamental has to box through IReferenceArray<T>, which NativeAOT cannot synthesise. The call throws NotSupportedException at runtime, often inside a catch that hides it.");

        public const string CollectionExpressionDiagnosticId = "TG1003";

        private static readonly DiagnosticDescriptor CollectionExpressionRule = new DiagnosticDescriptor(
            CollectionExpressionDiagnosticId,
            "Collection expression cannot cross the WinRT ABI",
            "A collection expression targeting '{0}' compiles to a synthesised read-only type, which can never have a CCW. Write new[] {{ … }} or new List<T> {{ … }} instead",
            "Interoperability",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Targeting a read-only interface, a collection expression compiles to <>z__ReadOnlySingleElementList<T> for one element and <>z__ReadOnlyArray<T> for more. Neither is a type anything can generate a vtable for, so the call fails at runtime with an InvalidCastException - and because the type depends on the element count, a site that works with two elements can break when one is removed. Targeting List<T> or IList<T> is safe: those produce a real List<T>.");

        // The type is only reachable in the CsWinRT build; on .NET Native the attribute does not
        // exist and neither does the problem, so the analyzer switches itself off there.
        private const string AttributeName = "WinRT.GeneratedWinRTExposedExternalTypeAttribute";

        // The value types WinRT boxes on its own, from the PropertyType enumeration. Anything else -
        // Color, a custom struct, an enum - has no IReferenceArray implementation to fall back on.
        private static readonly string[] FundamentalTypes =
        {
            "System.Boolean",
            "System.Byte",
            "System.Int16",
            "System.UInt16",
            "System.Int32",
            "System.UInt32",
            "System.Int64",
            "System.UInt64",
            "System.Single",
            "System.Double",
            "System.Char",
            "System.String",
            "System.Guid",
            "System.DateTimeOffset",
            "System.TimeSpan",
            "Windows.Foundation.Point",
            "Windows.Foundation.Size",
            "Windows.Foundation.Rect",
        };

        // What an untyped WinRT surface looks like once projected. IBindableIterable and
        // IBindableVector come through as the non-generic BCL interfaces and carry no element type
        // either, so they need the concrete vtable just as object does.
        private static readonly string[] UntypedSurfaces =
        {
            "System.Object",
            "System.Collections.IEnumerable",
            "System.Collections.IList",
        };

        // A collection expression targeting one of these does not produce a List: the interface is
        // read-only, so the compiler is free to synthesise a type of its own, and does. Targeting
        // List<T>, IList<T> or ICollection<T> is safe - a mutable interface needs a mutable
        // instance, and that is a real List<T>.
        private static readonly string[] ReadOnlySurfaces =
        {
            "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.IReadOnlyList`1",
            "System.Collections.Generic.IReadOnlyCollection`1",
        };

        // Typed collections, which the generator handles at the call site. Listed only so that the
        // elements can be followed: those are boxed later, out of sight of any call site. Note
        // IEnumerable<T> rather than IIterable<T> - by the time Roslyn sees the signature the
        // projection has already rewritten it.
        private static readonly string[] TypedSurfaces =
        {
            "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.IList`1",
            "System.Collections.Generic.ICollection`1",
            "System.Collections.Generic.IReadOnlyList`1",
            "System.Collections.Generic.IReadOnlyCollection`1",
            "System.Collections.Generic.IDictionary`2",
            "System.Collections.Generic.IReadOnlyDictionary`2",
        };

        private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, ReferenceArrayRule, CollectionExpressionRule);

        public override void Initialize(AnalysisContext context)
        {
            // Generated code included: x:Bind emits the ItemsSource assignment into a .g.cs, so
            // excluding it would hide every binding-driven case.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var attribute = context.Compilation.GetTypeByMetadataName(AttributeName);
            if (attribute == null)
            {
                return;
            }

            var state = new AnalysisState(context.Compilation, attribute);

            context.RegisterOperationAction(state.AnalyzeArgument, OperationKind.Argument);
            context.RegisterOperationAction(state.AnalyzeAssignment, OperationKind.SimpleAssignment);
        }

        private sealed class AnalysisState
        {
            private readonly HashSet<ITypeSymbol> _registered = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            private readonly HashSet<ITypeSymbol> _untyped = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            private readonly HashSet<ITypeSymbol> _typed = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            private readonly HashSet<ITypeSymbol> _readOnly = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            private readonly HashSet<ITypeSymbol> _fundamental = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            // Every argument of every call asks the same question of a few hundred types, and the
            // answer is an attribute walk.
            private readonly ConcurrentDictionary<ITypeSymbol, bool> _projected =
                new ConcurrentDictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

            public AnalysisState(Compilation compilation, INamedTypeSymbol attribute)
            {
                foreach (var declared in compilation.Assembly.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(declared.AttributeClass, attribute)
                        && declared.ConstructorArguments.Length > 0
                        && declared.ConstructorArguments[0].Value is ITypeSymbol type)
                    {
                        _registered.Add(type);
                    }
                }

                Populate(compilation, UntypedSurfaces, _untyped);
                Populate(compilation, TypedSurfaces, _typed);
                Populate(compilation, ReadOnlySurfaces, _readOnly);
                Populate(compilation, FundamentalTypes, _fundamental);
            }

            private static void Populate(Compilation compilation, string[] names, HashSet<ITypeSymbol> target)
            {
                foreach (var name in names)
                {
                    var surface = compilation.GetTypeByMetadataName(name);
                    if (surface != null)
                    {
                        target.Add(surface);
                    }
                }
            }

            public void AnalyzeArgument(OperationAnalysisContext context)
            {
                var operation = (IArgumentOperation)context.Operation;
                var parameter = operation.Parameter;

                if (parameter == null || !IsBoundary(parameter.ContainingSymbol))
                {
                    return;
                }

                Check(context, parameter.Type, operation.Value);
            }

            public void AnalyzeAssignment(OperationAnalysisContext context)
            {
                var operation = (ISimpleAssignmentOperation)context.Operation;

                if (operation.Target is IPropertyReferenceOperation property && IsProjected(property.Property.ContainingType))
                {
                    Check(context, property.Property.Type, operation.Value);
                }
            }

            private void Check(OperationAnalysisContext context, ITypeSymbol target, IOperation value)
            {
                // The conversion node carries the target type; the operand under it is what actually
                // gets a CCW at runtime.
                while (value is IConversionOperation conversion)
                {
                    value = conversion.Operand;
                }

                var type = value.Type;
                if (type == null)
                {
                    return;
                }

                // Before anything else: the synthesised types have no CCW and never will, so the
                // usual question of which instantiation to register does not apply.
                if (value.Kind == OperationKind.CollectionExpression && IsReadOnly(target))
                {
                    context.ReportDiagnostic(Diagnostic.Create(CollectionExpressionRule, value.Syntax.GetLocation(),
                        target.ToDisplayString(TypeFormat)));
                    return;
                }

                if (IsUntyped(target))
                {
                    if (NeedsVtable(type) && !_registered.Contains(type))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(),
                            type.ToDisplayString(TypeFormat), target.ToDisplayString(TypeFormat)));
                    }

                    // Everything in an untyped collection is boxed one element at a time, so the
                    // elements are the same question again.
                    Descend(context, target, value);
                    return;
                }

                if (Element(target) is not ITypeSymbol element)
                {
                    return;
                }

                // A typed collection is a conversion the generator can see, so the collection itself
                // is fine however it is spelled - unless it is an array of a value type, which has
                // to box through IReferenceArray whoever asked for it.
                if (type is IArrayTypeSymbol array && array.ElementType.IsValueType && !_fundamental.Contains(array.ElementType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ReferenceArrayRule, value.Syntax.GetLocation(),
                        array.ElementType.ToDisplayString(TypeFormat), target.ToDisplayString(TypeFormat)));
                    return;
                }

                // Elements are a different matter: GetAt marshals them at a point no call site
                // corresponds to, and the signature only names an interface, so the concrete type is
                // visible here and nowhere else.
                if (IsUntyped(element) || Element(element) != null)
                {
                    Descend(context, element, value);
                }
            }

            private void Descend(OperationAnalysisContext context, ITypeSymbol element, IOperation value)
            {
                foreach (var item in Elements(value))
                {
                    Check(context, element, item);
                }
            }

            private static IEnumerable<IOperation> Elements(IOperation value)
            {
                switch (value)
                {
                    case IArrayCreationOperation array when array.Initializer != null:
                        return array.Initializer.ElementValues;
                    case IObjectCreationOperation creation when creation.Initializer != null:
                        // List<T> { a, b } lands as a sequence of Add invocations.
                        return creation.Initializer.Initializers
                            .OfType<IInvocationOperation>()
                            .SelectMany(x => x.Arguments)
                            .Select(x => x.Value);
                    default:
                        return Enumerable.Empty<IOperation>();
                }
            }

            /// <summary>
            /// Whether crossing into this member means crossing into WinRT.
            /// </summary>
            /// <remarks>
            /// x:Bind does not assign the property itself: it calls a Set_ on the XamlBindingSetters
            /// the XAML compiler emits into the page, which takes the value as object and assigns it
            /// one frame later. That class belongs to this assembly and is projected by nothing, so
            /// following the call is the only way to see the concrete type - and the only reason the
            /// concrete type is there to see is that the property is declared as a List.
            /// </remarks>
            private bool IsBoundary(ISymbol member)
            {
                var declaring = member?.ContainingType;
                if (declaring == null)
                {
                    return false;
                }

                return IsProjected(declaring)
                    || (declaring.Name == "XamlBindingSetters" && member.Name.StartsWith("Set_", System.StringComparison.Ordinal));
            }

            private bool IsReadOnly(ITypeSymbol type)
            {
                return type is INamedTypeSymbol named && _readOnly.Contains(named.OriginalDefinition);
            }

            private bool IsUntyped(ITypeSymbol type)
            {
                return type is INamedTypeSymbol named && _untyped.Contains(named.OriginalDefinition);
            }

            /// <summary>
            /// The element type of a typed WinRT collection, or null if this is not one. For a map
            /// that is the value type: the key of an IMap is a string or an int in practice.
            /// </summary>
            private ITypeSymbol Element(ITypeSymbol type)
            {
                if (type is INamedTypeSymbol named && _typed.Contains(named.OriginalDefinition))
                {
                    return named.TypeArguments[named.TypeArguments.Length - 1];
                }

                return null;
            }

            private bool NeedsVtable(ITypeSymbol type)
            {
                if (IsOpen(type))
                {
                    // An open instantiation cannot be named in the attribute, and the closed ones
                    // are reported at whatever call site substitutes them.
                    return false;
                }

                if (type is IArrayTypeSymbol)
                {
                    return true;
                }

                if (type is not INamedTypeSymbol named || !named.IsGenericType)
                {
                    // CsWinRT generates a vtable for every non-generic type in this assembly, and a
                    // non-generic type from elsewhere is almost always a projected one that already
                    // has its own.
                    return false;
                }

                if (named.TypeKind != TypeKind.Class || named.IsAbstract)
                {
                    return false;
                }

                // Already a WinRT object on the other side of the boundary.
                return !IsProjected(named);
            }

            private static bool IsOpen(ITypeSymbol type)
            {
                switch (type)
                {
                    case ITypeParameterSymbol _:
                        return true;
                    case IArrayTypeSymbol array:
                        return IsOpen(array.ElementType);
                    case INamedTypeSymbol named:
                        foreach (var argument in named.TypeArguments)
                        {
                            if (IsOpen(argument))
                            {
                                return true;
                            }
                        }

                        return false;
                    default:
                        return false;
                }
            }

            private bool IsProjected(INamedTypeSymbol type)
            {
                if (type == null)
                {
                    return false;
                }

                return _projected.GetOrAdd(type.OriginalDefinition, WinRTProjection.IsProjected);
            }
        }
    }
}
