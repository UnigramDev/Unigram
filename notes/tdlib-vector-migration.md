# Exposing TDLib vectors as Vector<T>

**Done, 2026-08-28.** `vector<x>` in `td_api.tl` is generated as an immutable `Vector<T>`, with
`MutableVector<T>` for the callers that build one. The app runs. This note is the record of why,
because the reasons it was originally argued for all turned out to be wrong and the reason it paid
off was not predicted by anyone.

## The short version

- **It was argued for analyzer visibility.** That died twice over - see the two sections below.
- **It was worth doing for the allocation.** The parser materialised 25,361 empty `List<T>` a
  minute - 792 KB, 71.6% of all vectors - and a shared `Vector<T>.Empty` costs nothing. That win
  needs no custom type; `Array.Empty<T>()` in an `IList<T>` slot would do. What needs the type is
  the **audit**: the empty may only be shared if nothing writes to it, and this app is far too
  large to establish that by grep (two methods overlapped on 25 of 51 sites) or by exercising it
  (dozens of screens, unknowable coverage). Only the compiler can enumerate them.
- **The payoff nobody predicted**: what crosses the WinRT ABI became knowable again. Not through
  TG1001, but because the candidate set collapsed from unbounded (`List<T>`, `T[]`, anything
  implementing `IList<T>`) to exactly two named types, one of which - `MutableVector<T>`, 171
  mentions across 45 files - only exists where someone typed it. That is greppable.

## Why not just IReadOnlyList<T>

It would have got the allocation win identically: `Array.Empty<T>()` satisfies it, mutation is a
compile error, `.Count` works through `IReadOnlyCollection<T>`, and the compiler-driven audit is the
same. No new type, no `System.Numerics` clash, no new instantiations. Two things stop it:

- **The concrete type goes unbounded again**, which is the payoff above, lost.
- **Collection expressions become a WinRT hazard.** Measured: `[]` targeting `IReadOnlyList<T>`
  compiles to `String[]`, `[x]` to `<>z__ReadOnlySingleElementList<T>`, `[x, y]` to
  `<>z__ReadOnlyArray<T>` - synthesised types that can never have a CCW, and the type changes with
  the element count. That is exactly what TG1003 exists to catch.

## Why it came up

A binding assigns through the **declared** type. With `IList<T>` the concrete type exists only at
runtime, so TG1001 could never see it, `CsWinRT.Vectors.cs` had to blanket-register 188
instantiations, and every vector a new schema added looked like an open-ended blind spot -
`SettingsStoragePage` threw `E_INVALIDARG` with nothing to warn on.

**That premise does not survive**, for two independent reasons recorded below. It is left here
because it is what the work started from, and because the reasoning was sound while the conclusion
was not - which is the kind of mistake worth being able to retrace.
## The name

`System.Numerics.Vector<T>` exists and 216 files import `System.Numerics`. Verified by compiling:
a generic using alias is not legal C# at all (`global using Vector<T> = ...` is a syntax error),
and `global using Telegram.Td;` still yields CS0104. Declaring it in the **root `Telegram`
namespace** resolves it - an enclosing namespace is searched before any using directive, and every
file in the app is under `Telegram.*`. `Common/WatchDog.cs` and `Strings/en/Resources.cs` already
sit in subfolders under that namespace.

## What it was estimated to cost, before doing it

Build of `Telegram.Modern.csproj` with the generator flipped to emit `Vector<T>` for objects **and**
functions:

- 591 errors, of which **351 are in the generated `TdDotNetApi.g.cs` and are all one cause** - the
  parsers still return `List<T>`. One fix, not 351.
- **236 app sites across 70 files**: 159 argument conversions (95 from `List<T>`, 64 from
  `IList<T>`), 27 assignments, 32 mutations, 18 assorted.
- 57 of the 236 construct a *function*, so keeping functions on `IList<T>` would save about a
  quarter. Not taken: the split makes "functions are never bound" an invariant nobody can check.

Second build with `IList<T>` and the non-generic `IList` removed while `IReadOnlyList<T>` was kept,
so that every escape into a slot that *could* mutate fails to compile while `foreach` and LINQ stay
quiet:

- **448 app sites across 110 files** - 212 more.
- 192 of the new errors escape into `IList<…>`; **zero** into `IEnumerable<T>`,
  `IReadOnlyList<T>`, `IReadOnlyCollection<T>` or `ICollection<T>`, which is the check that the
  experiment isolated what it meant to.
- Resolved: 11 escape into a helper that mutates its `IList<T>` parameter, 8 are stored into a field
  mutated elsewhere, 140 into read-only sinks, 27 unresolved.

So roughly 236 direct edits plus 212 declaration tightenings. The tightenings are individually
trivial and are what buys TG1001 the visibility this is all for, but each needs a glance to confirm
the callee does not mutate.

## The mutation sites are the finding

70 places write into a list that came from TDLib - see [tdlib-vector-mutations.md]. Two views of the
direct ones overlap on only 25 of 51: **7 compiler-only, 19 grep-only**, so neither method alone
finds the list.

Two shapes are mixed together and want opposite treatment. `ClientService` mutating a cached object
under `lock (value)` to apply an update is deliberate and correct today; immutability turns each
into a rebuild, costing an allocation on the update path in exchange for readers getting a
consistent snapshot rather than a torn one. Everything in `Controls\` and most of `ViewModels\` is
the other shape - app code editing a list it was merely handed, on an object shared with the cache.

## What the parser needs

9 `Ptr` readers, 9 `Reader`-family readers, 8 `WriteArray` overloads.

- **Reads.** JSON arrays carry no length, so accumulate and hand over an exact array: grow a plain
  `T[]` by doubling and `Array.Resize` to exact at the end. `ArrayPool<T>.Shared` was considered and
  rejected - it is a static *per closed type*, so 189 element types means 189 pool object graphs,
  each with per-core locked stacks and a thread-static bucket array.
- **`stackalloc` does not apply.** It requires an unmanaged type (CS0208) and 184 of the 189 element
  types are reference types. `[InlineArray]` would work - verified, references in an inline array
  survive a GC - but it is a runtime *layout* feature ILC does not implement, so it is out on .NET
  Native. Only `vector<int32>` and `vector<int53>`/`int64` could use `stackalloc`, and only with a
  cap and a heap fallback, since the parser recurses and a stack buffer per frame multiplies with
  nesting depth.
- **The empty case is the prize.** Today every optional vector allocates a `List<T>` to say "nothing
  here". `Vector<T>.Empty` makes that zero, on every object of every update, and drops `List<T>`'s
  retained up-to-2x capacity slack besides.
- **Writes get simpler.** `WriteArray`'s `is List<T>` / `is T[]` / `foreach` triple collapses to one
  loop over `AsSpan()`: no type test, no interface dispatch, bounds checks eliminable.

## CsWinRT 3.0 deletes the whole mechanism

Checked 2026-08-27 against `3.0.0-preview.260319.2` (published 2026-03-25) and its
[spec](https://github.com/microsoft/CsWinRT/blob/716eeb58fa61d7a053be41d13b09b7ff460733df/docs/cswinrt3.0-spec.md).

`GeneratedWinRTExposedExternalType`, `WinRTExposedType` and `RegisterTypeComInterfaceEntriesLookup`
are **gone** from both the runtime and the generator. The registration table is replaced by a
post-build **IL analysis** tool producing a `WinRT.Interop.dll` sidecar, which per the spec generates
marshalling code "with a global program view" covering "all marshalling code (including CCW support)
for generic type instantiations", "all WinRT-exposed types (user-defined and not)", and explicitly
"internal/synthesized types too (just like .NET Native could)". And: "the proxy type map in
particular means **no user action is needed to enable marshalling**".

Being a post-build IL tool rather than a source generator, it does not have the
generator-cannot-see-generator constraint at all - by then the SchemaGenerator's output is compiled
and resolved. So the problem this whole investigation circled is fixed upstream, at the root.

What that means here:

- `CsWinRT.Vectors.cs` and most of `CsWinRT.cs` become unnecessary.
- TG1001 and TDAPI003 both lose their purpose.
- The 1967-comparison string lookup goes: "0 overhead, 0 allocation for all vtables in the entire
  application domain", pre-initialised by ILC into `.rdata`.
- The 12 inert entries stop mattering, since nothing is inferred from syntax any more.

And it settles the type question rather than reopening it. The fix removes the registration rather
than making inference smarter, so a concrete declared type buys nothing: `IList<T>` marshals fine
because the sidecar reads the real instantiations out of IL, not out of a declaration.

Not to be chased now. It is a first preview, an explicitly breaking release, and adoption needs a new
TFM (`net10.0-windows10.0.26100.0` -> `...26100.1`). 2.x is still maintained - tag `2.3.1.260716.1`
postdates the 3.0 preview. Note also that 3.0 projects WinRT `T[]` parameters as
`ReadOnlySpan<T>`/`Span<T>`, source compatible via C# 14 first-class spans but touching the
`Telegram.Native` interop surface.

## Why the visibility argument fails - the finding that decides it

Source generators all run against the same input compilation, so **CsWinRT's optimizer cannot
resolve anything the SchemaGenerator produced**. When it infers a registration from a call site
touching a TDLib type it has only the syntax to work from, so it writes the bare name into
`WinRTGlobalVtableLookup.g.cs` - and `Type.ToString()` always produces the namespace-qualified form,
so the entry can never match. Measured on a Release build: of the 41 distinct unqualified element
types in that lookup, **40 are types the SchemaGenerator emits**; the one exception is `TResult`, an
open generic parameter. Every one of those entries is dead on arrival. The 466 qualified
`Telegram.Td.Api.*` entries are the ones a human spelled out in `CsWinRT.Vectors.cs`.

The declared type changes none of this. `IList<T>`, `List<T>` and `Vector<T>` are equally invisible
to CsWinRT for the same reason, so no type buys an inferred registration.

TG1001 is an analyzer rather than a generator, so it does see generated code and would still report
the reachable set. But acting on that means replacing a blanket list that is **complete by
construction** with a hand-maintained subset derived from an analyzer already known to miss things -
the 12 inert entries, the 83 app collections. That trades binary size for a class of runtime
`E_INVALIDARG` the blanket file structurally cannot have.

So `CsWinRT.Vectors.cs` is not a workaround waiting to be improved. Given generator-to-generator
blindness it is the correct design: the one mechanism that cannot miss a TDLib vector.

## The shape it landed in

`Telegram/Td/Vector.cs` holds both types.

- `Vector<T> : IReadOnlyList<T>` - no mutating member at all, so a write is a compile error rather
  than a runtime throw, and passing one where an `IList<T>` is expected does not compile either.
  That last part is what made the escape points enumerable.
- `MutableVector<T> : Vector<T>, IList<T>, IList` - for building a request argument, and for the
  reused-buffer case (`ChatView._viewVisibleMessages` into `ViewMessages`, refilled per scroll)
  where a copy per call would be a real regression. `Client.Send` serialises synchronously, so the
  callee cannot observe a later edit.
- Not sealed, so `Count` comes from a `_count` field rather than the array length. Costs one int -
  24 to 32 bytes an instance, parity with `List<T>` - and one compare on the indexer. Measured.
- `Vector<T>.Empty` per element type; `Array.Empty<T>()` and `[]` both fold to it. The implicit
  conversion from `T[]` wraps without copying, so ~134 construction sites compiled unchanged.
- The name has to stay in the root `Telegram` namespace: `System.Numerics.Vector<T>` clashes,
  generic using aliases are not legal C# (verified), and an enclosing namespace beats a using.

## Measured

`TdVectorStats`, one minute of use, before the change:

```
vectors parsed : 35,424
empty          : 25,361 (71.6 %) = 792 KB of List<T> holding nothing

ChatPosition     5,198/5,213 empty  162 KB   0 mutation sites
Int32            4,866/6,162        152 KB   unattributed
String           3,794/5,879        118 KB   unattributed
TextEntity       3,559/4,423        111 KB   0 after FormattedTextBuilder
UnreadReaction   3,299/3,300        103 KB   6 sites
ChatList         2,709/2,713         84 KB   2 sites
```

`Int32`/`String` are 270 KB the element-type granularity cannot attribute - the parser is generic
over the element and never learns the field. Adding the field name to the reader signatures would
close it.

## Still open

- **`TdVectorStats.Count` was lost from all nine readers** in the rewrite. There is a before
  snapshot and no after. Restore the calls before the tree drifts further.
- **`CsWinRT.Vectors.cs` needs both halves reworked.** Of its 182 `List<TdType>` registrations, 23
  are still live (app code that builds its own `List<Chat>` and binds it) and 159 are dead. The
  `Vector<X>` set that replaces them does not exist yet. Deciding it needs the ItemsSource question
  below answered first.
- **`ItemsSource` binding is untested.** `Vector<T>` no longer implements non-generic `IList`, which
  is what `IBindableVector` projects from. Non-generic `IEnumerable` survives via `IEnumerable<T>`,
  so `IBindableIterable` should be enough - but open an Instant View and a bound TDLib list before
  believing it. If it is not, the fallback is `Vector<T> : IReadOnlyList<T>, IList`, which still
  makes every `IList<T>` escape a compile error.
- **`MutableVector<T>` escapes into TDLib objects.** `MarkdownToInstantView` and `RichHtml` build
  `PageBlock` trees whose `Blocks`/`Items`/`Cells` are mutable instances. Finishing those builders
  with `.ToVector()` would keep the registered set to one type per element instead of two.
- **`SettingsStorageViewModel`**: `result.ByFileType.Add(already)` became `chatByFileType.Add(...)`
  during the port, so the aggregate is now never populated. Looks like a rename applied one
  identifier too far.
- **The Reader parser family is ported but never compiled.** `TdParsers=Reader` should build again;
  it has not been tried.
- **`Naming.cs` must land with the migration**, not before. Alone it emits `Vector<T>` against
  consumers that still expect `IList<T>`.

## Traps worth knowing

- **A reader's empty guard and its loop condition must agree.** `GetStringArrayPtr` guarded on
  `String` and looped on `Number`, so a two-element string array parsed one element and left the
  reader mid-array. It surfaced as `updateChatLastMessage.Positions` being null - four levels up,
  a different field, no exception anywhere. Cross-check the nine pairs after any parser edit.
- **`is Vector<T>` no longer means immutable**, since `MutableVector<T>` satisfies it. `ToVector()`
  and the `Vector(IEnumerable<T>)` fast path both check `GetType() == typeof(Vector<T>)` before
  sharing an array. Anything else doing a type test needs the same care.
- **`Empty()` and `FindIndex()`-style helpers**: an `ICollection<T>` and an `IReadOnlyCollection<T>`
  overload pair is unfixable, because `List<T>`, arrays and `MutableVector<T>` implement both and
  neither converts to the other. One `IEnumerable<T>` overload with type tests, plus an exact
  `Vector<T>` overload for the fast path - that pair is asymmetric, so it resolves.
