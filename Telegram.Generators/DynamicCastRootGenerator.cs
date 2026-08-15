//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Telegram.Generators
{
    /// <summary>
    /// Roots every projected WinRT class the app casts to, so that trimming cannot take the metadata
    /// the cast needs.
    /// </summary>
    /// <remarks>
    /// Without the root, an <c>as</c> yields null rather than throwing, and a null template child
    /// reads as a feature quietly doing nothing: ProgressBar cost a day of an exception per frame,
    /// and ItemsPresenter the chat list's folder swipe. CsWinRT reports each one as CsWinRT1034 and
    /// asks for a [DynamicWindowsRuntimeCast] on the containing method - roughly 1800 of them here,
    /// and a new cast is a new silent null.
    ///
    /// This emits the same thing CsWinRT's own generator emits for those attributes, from the casts
    /// themselves. Not the attributes: generators all see the original compilation, so nothing this
    /// one writes would reach CsWinRT's.
    ///
    /// It emits nothing when the attributes it needs are missing, which is what happens on .NET
    /// Native - where nothing is trimmed and there is nothing to root.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class DynamicCastRootGenerator : IIncrementalGenerator
    {
        private const string ModuleInitializerAttribute = "System.Runtime.CompilerServices.ModuleInitializerAttribute";
        private const string DynamicDependencyAttribute = "System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var types = context.SyntaxProvider
                .CreateSyntaxProvider(IsCast, GetCastType)
                .Where(x => x != null)
                .Collect();

            var supported = context.CompilationProvider.Select(IsSupported);

            context.RegisterSourceOutput(types.Combine(supported), Execute);
        }

        private static bool IsCast(SyntaxNode node, System.Threading.CancellationToken token)
        {
            switch (node)
            {
                case BinaryExpressionSyntax binary:
                    // Both "x as T" and "x is T", which take the same path through the RCW factory.
                    return binary.IsKind(SyntaxKind.AsExpression) || binary.IsKind(SyntaxKind.IsExpression);
                case CastExpressionSyntax _:
                case DeclarationPatternSyntax _:
                    return true;
                default:
                    return false;
            }
        }

        private static string GetCastType(GeneratorSyntaxContext context, System.Threading.CancellationToken token)
        {
            TypeSyntax syntax;
            switch (context.Node)
            {
                case BinaryExpressionSyntax binary:
                    syntax = binary.Right as TypeSyntax;
                    break;
                case CastExpressionSyntax cast:
                    syntax = cast.Type;
                    break;
                case DeclarationPatternSyntax pattern:
                    syntax = pattern.Type;
                    break;
                default:
                    return null;
            }

            if (syntax == null)
            {
                return null;
            }

            var type = context.SemanticModel.GetTypeInfo(syntax, token).Type;

            // Only a runtime class has an RCW factory to lose. Interfaces resolve through the vtable
            // the projection always keeps, and structs and enums are copied across by value.
            if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class || named.IsGenericType)
            {
                return null;
            }

            if (!WinRTProjection.IsProjected(named))
            {
                return null;
            }

            return named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static bool IsSupported(Compilation compilation, System.Threading.CancellationToken token)
        {
            return compilation.GetTypeByMetadataName(ModuleInitializerAttribute) != null
                && compilation.GetTypeByMetadataName(DynamicDependencyAttribute) != null;
        }

        private static void Execute(SourceProductionContext context, (ImmutableArray<string> Types, bool Supported) input)
        {
            if (!input.Supported)
            {
                return;
            }

            var names = new SortedSet<string>(input.Types.Where(x => x != null));
            if (names.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("// <auto-generated/>\r\n");
            builder.Append("#pragma warning disable\r\n");
            builder.Append("\r\n");
            builder.Append("namespace Telegram\r\n");
            builder.Append("{\r\n");
            builder.Append("    internal static class DynamicCastRoots\r\n");
            builder.Append("    {\r\n");
            builder.Append("        [global::System.Runtime.CompilerServices.ModuleInitializer]\r\n");

            foreach (var name in names)
            {
                // The members CsWinRT's own initializer keeps: the RCW's non-public constructor, and
                // the fields the marshaller reads.
                builder.Append("        [global::System.Diagnostics.CodeAnalysis.DynamicDependency(");
                builder.Append("global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors | ");
                builder.Append("global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields, ");
                builder.Append("typeof(");
                builder.Append(name);
                builder.Append("))]\r\n");
            }

            builder.Append("        internal static void Initialize()\r\n");
            builder.Append("        {\r\n");
            builder.Append("        }\r\n");
            builder.Append("    }\r\n");
            builder.Append("}\r\n");

            context.AddSource("DynamicCastRoots.g.cs", builder.ToString());
        }
    }
}
