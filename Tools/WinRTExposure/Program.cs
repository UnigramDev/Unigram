using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// Finds the places that need [assembly: GeneratedWinRTExposedExternalType].
//
// A managed object crossing the WinRT ABI needs a CCW vtable for its EXACT runtime type. CsWinRT's
// generator emits one for every instantiation it can see converted in source - so the gap is the
// sites where the type it sees is not the type that arrives:
//
//   A  a member declared as one generic type and constructed as another
//   B  a non-concrete type (interface or abstract) reaching a WinRT sink such as ItemsSource
//
// Roslyn syntax, no binding: exact for an explicit declaration paired with an explicit `new`, and
// blind to anything that needs a symbol - a factory method's return type, var, a cast. Treat the
// output as the list to look at, not as the list of defects. The compilation-bound version of this
// is WinRTExposedTypeAnalyzer (TG1001/TG1002) in Telegram.Generators.WinRT.

var root = args.FirstOrDefault(x => !x.StartsWith('-')) ?? @"C:\Source\Telegram\Telegram";
var only = args.FirstOrDefault(x => x is "-all");

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"not a directory: {root}");
    return 1;
}

// Absolute from here on: a relative argument ("Telegram") has no parent to report paths against.
root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);

// Paths are printed relative to the directory ABOVE the root, so they read as Telegram\Views\... -
// the repo-relative form, which is what a file:line reference wants.
var display = Path.GetDirectoryName(root) ?? root;

// The WinRT-typed properties a managed object is handed to. ItemsSource is the one that bites,
// but anything typed object across the ABI boxes the same way.
var sinks = new HashSet<string>(StringComparer.Ordinal)
{
    "ItemsSource", "SelectedItems", "SelectedItem", "Content", "DataContext", "Source"
};

// The manifest as it stands, so a hit can say whether it is already covered rather than only that
// it exists. Namespaces are stripped from both sides: the attribute spells types out, source does not.
var registered = new HashSet<string>(StringComparer.Ordinal);

foreach (var manifest in Directory.EnumerateFiles(root, "CsWinRT*.cs", SearchOption.TopDirectoryOnly))
{
    // Parsed, not matched: a commented-out attribute is trivia and never becomes an AttributeSyntax,
    // so a manifest that has been disabled for an experiment reads as disabled. The symbol has to be
    // defined or the whole #if NET9_0_OR_GREATER body is inactive text and nothing is found at all.
    var options = new CSharpParseOptions(preprocessorSymbols: ["NET9_0_OR_GREATER"]);
    var unit = CSharpSyntaxTree.ParseText(File.ReadAllText(manifest), options).GetCompilationUnitRoot();

    foreach (var attribute in unit.AttributeLists
        .Where(x => x.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true)
        .SelectMany(x => x.Attributes))
    {
        if (!attribute.Name.ToString().EndsWith("GeneratedWinRTExposedExternalType", StringComparison.Ordinal))
        {
            continue;
        }

        if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf)
        {
            registered.Add(Simplify(typeOf.Type.ToString()));
        }
    }
}

// The generated lookup is the real answer: the manifest is only one of the ways an instantiation
// gets a vtable, and CsWinRT discovers most of them from conversions it can see. Absent from BOTH
// is what actually matters.
var vtabled = new HashSet<string>(StringComparer.Ordinal);

var lookup = Directory.EnumerateFiles(root, "WinRTGlobalVtableLookup.g.cs", SearchOption.AllDirectories)
    .OrderByDescending(File.GetLastWriteTimeUtc)
    .FirstOrDefault();

if (lookup != null)
{
    foreach (Match match in Regex.Matches(File.ReadAllText(lookup), @"typeName == ""([^""]*)"""))
    {
        vtabled.Add(Simplify(FromClrName(match.Groups[1].Value)));
    }
}

var sinkUses = new List<SinkUse>();
var declarations = new Dictionary<string, Decl>();      // Owner+Member -> declaration
var constructions = new List<Construction>();
var interfaces = new HashSet<string>(StringComparer.Ordinal);
var abstracts = new HashSet<string>(StringComparer.Ordinal);
var viewModelOf = new Dictionary<string, string>(StringComparer.Ordinal);   // page class -> ViewModel type

// Type -> its plain CLR properties, and Type -> its DependencyProperties. A binding to a plain CLR
// property on a managed control is a direct managed call and never reaches the ABI; only a
// DependencyProperty (SetValue takes IInspectable) or a property on a framework type does.
var clrProperties = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
var dependencyProperties = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

// The BCL collection interfaces a binding is most often declared as. Repo-declared interfaces are
// discovered below instead of listed, so this only has to cover what comes from outside.
var frameworkInterfaces = new HashSet<string>(StringComparer.Ordinal)
{
    "IList", "IReadOnlyList", "IEnumerable", "ICollection", "IReadOnlyCollection",
    "IDictionary", "IReadOnlyDictionary", "ISet", "IObservableVector", "IBindableVector"
};

foreach (var path in EnumerateSource(root, "*.cs"))
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
    var unit = tree.GetCompilationUnitRoot();

    foreach (var type in unit.DescendantNodes().OfType<TypeDeclarationSyntax>())
    {
        var owner = OwnerKey(type);

        if (type is InterfaceDeclarationSyntax)
        {
            interfaces.Add(type.Identifier.Text);
        }
        else if (type.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            abstracts.Add(type.Identifier.Text);
        }

        foreach (var member in type.Members)
        {
            if (member is PropertyDeclarationSyntax p)
            {
                Declare(p.Type, p.Identifier, p.Initializer, p.Modifiers);

                if (!clrProperties.TryGetValue(type.Identifier.Text, out var declared))
                {
                    clrProperties[type.Identifier.Text] = declared = new HashSet<string>(StringComparer.Ordinal);
                }

                declared.Add(p.Identifier.Text);

                // A page's ViewModel property is how an {x:Bind ViewModel.X} is resolved later.
                if (p.Identifier.Text == "ViewModel")
                {
                    viewModelOf[type.Identifier.Text] = Bare(Norm(p.Type));
                }
            }
            else if (member is FieldDeclarationSyntax f)
            {
                foreach (var v in f.Declaration.Variables)
                {
                    Declare(f.Declaration.Type, v.Identifier, v.Initializer, f.Modifiers);
                }
            }

            void Declare(TypeSyntax declared, SyntaxToken id, EqualsValueClauseSyntax init, SyntaxTokenList mods)
            {
                if (declared is null || !declared.ToString().Contains('<'))
                {
                    return;
                }

                var access = mods.Any(SyntaxKind.PublicKeyword) ? "public"
                    : mods.Any(SyntaxKind.InternalKeyword) ? "internal"
                    : mods.Any(SyntaxKind.ProtectedKeyword) ? "protected" : "private";

                declarations[owner + "." + id.Text] = new Decl(path, Line(tree, id.Span), owner,
                    id.Text, Norm(declared), access);

                if (init?.Value is ObjectCreationExpressionSyntax created)
                {
                    constructions.Add(new Construction(owner + "." + id.Text, Norm(created.Type)));
                }
            }
        }

        // DependencyProperty.Register(nameof(Items), ...) or Register("Items", ...)
        foreach (var invocation in type.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access
                || !access.Name.Identifier.Text.StartsWith("Register", StringComparison.Ordinal)
                || access.Expression.ToString() is not ("DependencyProperty" or "Windows.UI.Xaml.DependencyProperty"))
            {
                continue;
            }

            var first = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var name = first switch
            {
                InvocationExpressionSyntax nameOf when nameOf.Expression.ToString() == "nameof"
                    => nameOf.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } inner
                        ? NameOf(inner) : null,
                LiteralExpressionSyntax literal => literal.Token.ValueText,
                _ => null
            };

            if (name != null)
            {
                if (!dependencyProperties.TryGetValue(type.Identifier.Text, out var set))
                {
                    dependencyProperties[type.Identifier.Text] = set = new HashSet<string>(StringComparer.Ordinal);
                }

                set.Add(name);
            }
        }

        // Attributed to the NEAREST enclosing type: DescendantNodes walks into nested types, and a
        // nested class assigning its own member must not read as the outer class assigning one.
        foreach (var assign in type.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assign.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                continue;
            }

            var target = NameOf(assign.Left);

            if (assign.Right is ObjectCreationExpressionSyntax created
                && assign.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() == type
                && target != null)
            {
                constructions.Add(new Construction(owner + "." + target, Norm(created.Type)));
            }

            // Check B, the C# half: control.ItemsSource = something.
            if (target is not null && sinks.Contains(target) && assign.Right is not ObjectCreationExpressionSyntax)
            {
                var source = NameOf(assign.Right);
                if (source != null)
                {
                    sinkUses.Add(new SinkUse(path, Line(tree, assign.Span), target, source,
                        OwnerOf(assign.Right) ?? owner));
                }
            }
        }
    }
}

foreach (var path in EnumerateSource(root, "*.xaml"))
{
    XDocument document;

    try
    {
        document = XDocument.Load(path, LoadOptions.SetLineInfo);
    }
    catch (Exception)
    {
        // A .xaml that does not parse as XML is not this tool's problem; the XAML compiler reports it.
        continue;
    }

    foreach (var element in document.Descendants())
    {
        foreach (var attribute in element.Attributes())
        {
            // A binding only reaches the ABI if the target is a DependencyProperty or lives on a
            // framework type. A plain CLR property on one of our own controls is set by a direct
            // managed call - StorageChart.Items is the example that made this rule necessary.
            var targetType = element.Name.LocalName;
            var targetProperty = attribute.Name.LocalName;

            if (clrProperties.TryGetValue(targetType, out var plain)
                && plain.Contains(targetProperty)
                && !(dependencyProperties.TryGetValue(targetType, out var deps) && deps.Contains(targetProperty)))
            {
                continue;
            }

            var value = attribute.Value.Trim();
            if (!value.StartsWith("{x:Bind") && !value.StartsWith("{Binding"))
            {
                continue;
            }

            var path_ = BindingPath(value);
            if (path_ == null)
            {
                continue;
            }

            var page = Path.GetFileNameWithoutExtension(path);
            var parts = path_.Split('.');

            // "ViewModel.Items" resolves against the page's ViewModel type; "Items" against the page.
            var owner = parts.Length > 1 && parts[0] == "ViewModel" && viewModelOf.TryGetValue(page, out var vm)
                ? vm
                : page;

            sinkUses.Add(new SinkUse(path, LineOf(element), attribute.Name.LocalName, parts[^1], owner));
        }
    }
}

// ---- what actually matters: a sink whose static type is not the runtime type ----------------

var builtAs = constructions
    .GroupBy(x => x.Key)
    .ToDictionary(g => g.Key, g => g.Select(x => x.CreatedType).Distinct().ToList());

var atSink = sinkUses
    .Select(u => declarations.TryGetValue(u.Owner + "." + u.Member, out var d) ? (Use: u, Decl: d) : default)
    .Where(x => x.Decl is not null)
    .Select(x => (x.Use, x.Decl, Built: builtAs.TryGetValue(x.Decl.Owner + "." + x.Decl.Member, out var b) ? b : []))
    .Where(x => IsNonConcrete(x.Decl.DeclaredType)
             || x.Built.Any(c => c.Contains('<') && Bare(c) != Bare(x.Decl.DeclaredType)))
    .GroupBy(x => x.Decl.Owner + "." + x.Decl.Member)
    .Select(g => g.First())
    .OrderBy(x => x.Decl.Owner, StringComparer.Ordinal)
    .ToList();

Console.WriteLine($"Static type is not the runtime type, AT A SINK  ({atSink.Count} of {sinkUses.Count} resolved bindings)");
Console.WriteLine(lookup is null
    ? "   no WinRTGlobalVtableLookup.g.cs found - build first to get vtable coverage"
    : $"   vtable checked against {Relative(lookup, display)}");
Console.WriteLine(new string('-', 118));

foreach (var (use, decl, built) in atSink)
{
    var why = IsNonConcrete(decl.DeclaredType) ? "non-concrete" : "mismatch";
    var runtime = built.Count > 0 ? string.Join(" | ", built) : "unknown - not constructed with new in this type";
    var cover = built.Count == 0 ? "?" : built.All(c => vtabled.Contains(Simplify(c))) ? "vtable"
        : built.All(c => vtabled.Contains(Simplify(c)) || registered.Contains(Simplify(c))) ? "manifest" : "MISSING";

    Console.WriteLine($"{cover,-8} {why,-13} {decl.Owner}.{decl.Member}");
    Console.WriteLine($"{"",-8} declared {decl.DeclaredType}");
    Console.WriteLine($"{"",-8} runtime  {runtime}");
    Console.WriteLine($"{"",-8} {Relative(decl.File, display)}:{decl.Line}   bound at {Relative(use.File, display)}:{use.Line} ({use.Sink})");
    Console.WriteLine();
}

if (only == "-all")
{
    var all = constructions
        .Select(c => declarations.TryGetValue(c.Key, out var d) ? (Decl: d, c.CreatedType) : default)
        .Where(x => x.Decl is not null && x.CreatedType.Contains('<'))
        .Where(x => Bare(x.Decl.DeclaredType) != Bare(x.CreatedType))
        .GroupBy(x => x.Decl.Owner + "." + x.Decl.Member + "|" + x.CreatedType)
        .Select(g => g.First())
        .OrderBy(x => x.Decl.DeclaredType, StringComparer.Ordinal)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Every declared/constructed mismatch, sink or not  ({all.Count} of {declarations.Count} generic members)");
    Console.WriteLine(new string('-', 118));

    foreach (var (decl, created) in all)
    {
        var key = Simplify(created);
        var mark = vtabled.Contains(key) ? "vtable " : registered.Contains(key) ? "manifest" : "MISSING";
        Console.WriteLine($"{mark} {decl.Accessibility,-9} {Trim(decl.DeclaredType, 42),-42} <- {Trim(created, 42),-42} {Relative(decl.File, display)}:{decl.Line}");
    }
}

return 0;

// ---- helpers -------------------------------------------------------------------------------

static IEnumerable<string> EnumerateSource(string root, string pattern) =>
    Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                 && !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                 && !x.EndsWith(".g.cs", StringComparison.Ordinal));

static int Line(SyntaxTree tree, TextSpan span) => tree.GetLineSpan(span).StartLinePosition.Line + 1;

static int LineOf(XElement element) => (element as IXmlLineInfo)?.LineNumber ?? 0;

static string Norm(TypeSyntax type) => type?.ToString().Replace(" ", "") ?? "";

static string Relative(string path, string baseDirectory) => Path.GetRelativePath(baseDirectory, path);

static string Trim(string value, int width) => value.Length <= width ? value : value[..(width - 1)] + "\u2026";

// The bare generic name, so Telegram.Collections.IncrementalCollection<X> and IncrementalCollection<X>
// compare equal - a using directive must not read as a difference.
static string Bare(string type)
{
    var lt = type.IndexOf('<');
    var head = lt < 0 ? type : type[..lt];
    var dot = head.LastIndexOf('.');

    if (dot >= 0)
    {
        head = head[(dot + 1)..];
    }

    return lt < 0 ? head : head + type[lt..];
}

// Namespace-free form of a type expression, type arguments included, so IncrementalCollection<Passkey>
// and Telegram.Collections.IncrementalCollection<Telegram.Td.Api.Passkey> compare equal.
static string Simplify(string type) =>
    Regex.Replace(type.Replace(" ", ""), @"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+", m => m.Value[(m.Value.LastIndexOf('.') + 1)..]);

// "System.Collections.Generic.List`1[Telegram.Td.Api.Color]" -> "System.Collections.Generic.List<Telegram.Td.Api.Color>"
static string FromClrName(string name) =>
    Regex.Replace(name, @"`\d+\[", "<").Replace("]", ">");

static string OwnerKey(TypeDeclarationSyntax type)
{
    var names = new List<string>();

    for (SyntaxNode node = type; node != null; node = node.Parent)
    {
        if (node is TypeDeclarationSyntax declaration)
        {
            names.Insert(0, declaration.Identifier.Text);
        }
    }

    return string.Join("+", names);
}

static string NameOf(ExpressionSyntax expression) => expression switch
{
    IdentifierNameSyntax i => i.Identifier.Text,
    MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
    _ => null
};

// "ViewModel.Items" on the right of a sink assignment names the type that owns Items.
static string OwnerOf(ExpressionSyntax expression) => expression is MemberAccessExpressionSyntax m
    ? NameOf(m.Expression)
    : null;

static string BindingPath(string markup)
{
    var body = markup.TrimStart('{');
    body = body.StartsWith("x:Bind") ? body[6..] : body.StartsWith("Binding") ? body[7..] : body;
    body = body.TrimEnd('}').Trim();

    if (body.StartsWith("Path=", StringComparison.Ordinal))
    {
        body = body[5..];
    }

    var end = body.IndexOfAny([',', ' ', '(', ')']);
    if (end >= 0)
    {
        body = body[..end];
    }

    return string.IsNullOrWhiteSpace(body) || body.Contains(':') ? null : body;
}

bool IsNonConcrete(string type)
{
    var name = Bare(type);
    var lt = name.IndexOf('<');
    var head = lt < 0 ? name : name[..lt];

    return interfaces.Contains(head) || abstracts.Contains(head) || frameworkInterfaces.Contains(head);
}

record Decl(string File, int Line, string Owner, string Member, string DeclaredType, string Accessibility);
record Construction(string Key, string CreatedType);
record SinkUse(string File, int Line, string Sink, string Member, string Owner);
