# Telegram.Benchmarks

Measures the TDLib JSON path — what `Client.Receive` and `ClientJson.FromJson` actually cost per
payload — so changes to it can be argued from numbers instead of intuition.

**Picking this up again? Read [Where this stands](#where-this-stands) at the bottom first.** It has
the state of the work and what is next; everything between here and there is how the numbers were
arrived at.

Not in `Telegram.sln` on purpose. It builds and runs on its own and never touches `Telegram.csproj`.

Three hosts, one `Suite.cs`: this desktop console, a UWP app on .NET 10 / NativeAOT, and a UWP app
on .NET Native — the last being what the shipping app runs, and the only one whose numbers should
drive a decision.

```
dotnet run -f net10.0 -c Release -- --validate-only   # parse the corpus, check the results
dotnet run -f net10.0 -c Release -- --plain           # the portable suite, incl. tdjson round trips
dotnet run -f net10.0 -c Release -- --filter "*"      # BenchmarkDotNet, everything (~4 min)
```

`-f net10.0` is required now that the project multi-targets.

Two harnesses on purpose. BenchmarkDotNet is the rigorous one and its numbers are the ones to
quote — but it spawns processes and emits IL, so it can't run under an AOT UWP host. `--plain`
runs `Suite.cs` through the hand-rolled `Harness`, which is exactly what the UWP host runs, so the
two runtimes can be compared row for row.

## The UWP host

One project, two target frameworks: `net10.0` is this console, `net10.0-windows10.0.26100.0` with
`<UseUwp>true</UseUwp>` is a UWP app that runs the same `Suite`. BenchmarkDotNet and `Program.cs`
are compiled out of the UWP target; `Uwp\UwpHost.cs` replaces them. No XAML files — the whole app
is one `TextBlock` — which keeps the XAML compiler out of the build.

UWP on .NET 9+ publishes with NativeAOT, so this measures AOT codegen rather than the JIT:

```
dotnet publish -f net10.0-windows10.0.26100.0 -c Release -r win-x64
pwsh tools\Stage.ps1                                        # loose-file package layout
Add-AppxPackage -Register ...\win-x64\publish\AppxManifest.xml
Start-Process "shell:appsFolder\Telegram.Benchmarks_k580v1fv7e4c6!App"
```

Results land in the app's `LocalState\report.txt` as well as on screen, because there's no console.
`Add-AppxPackage` needs the app closed first, and the native link step needs `vswhere.exe` on PATH
(`C:\Program Files (x86)\Microsoft Visual Studio\Installer`) or the ILCompiler can't find `link.exe`.

Two things it does **not** answer. It's the .NET 10 UWP stack, not the .NET Native toolchain the app
ships on today — so it says "where would we be", not "where are we". And because it's .NET 10, it
gets System.Text.Json's in-box `net10.0` implementation, while the app resolves that same 10.0.10
package's `netstandard2.0` asset. Isolating that difference still wants a reference to the
netstandard2.0 DLL straight out of the NuGet cache.

`tdjson.dll` is built for the store, so the package carries `vcruntime140_app.dll` and friends
(`Stage.ps1` copies them). A real package would declare a `Microsoft.VCLibs.140.00` dependency
instead. To uninstall: `Get-AppxPackage Telegram.Benchmarks | Remove-AppxPackage`.

## What it measures against

The real generated parser, not a copy of it: the project references `Telegram.Generators` as an
analyzer and feeds it the same `Libraries\tdjson\td_api.tl`, then links `ClientJson.cs` and
`ArrayPoolBufferWriter.cs` from the app. Change the generator and the numbers move. The generated
`TdDotNetApi.g.cs` is written to `obj\Release\net10.0\generated\` if you want to read it.

`Program.cs` validates before it measures: every corpus payload has to parse into the expected
object, and any candidate parser has to agree with the current one field for field. A faster parser
that returns different objects isn't a faster parser.

## The corpus

`Corpus\*.jsonl`, one payload per line. `synthetic.jsonl` is generated from `td_api.tl` and shows up
in results with a `~` prefix. It's faithful in shape: TDLib's generated `to_json` writes *every*
scalar field including `false` booleans and empty strings, and omits only null object pointers, so
the synthetic payloads carry the same field count and the same ratio of noise to content as the
wire does. It also includes the ~30 `; for bots only` fields the C# side deliberately drops, which
is what exercises the unknown-field path.

Real captures are better. Drop them in as `Corpus\capture.jsonl` (any name that isn't
`synthetic*`) and they'll be picked up without the `~`. To capture, in `Client.Receive`, after the
length is known and before `FromJson`:

```csharp
#if CAPTURE_CORPUS
System.IO.File.AppendAllText(@"C:\capture.jsonl",
    System.Text.Encoding.UTF8.GetString(_buffer, 0, length) + "\n");
#endif
```

JSON can't contain a raw newline, so one payload per line always holds.

## Results, 2026-08-13

`net10.0`, x64 RyuJIT, i9-13950HX. **Read the .NET Native section below before acting on any of
this** — that host runs ~3× slower and reverses one of the results outright.

End to end, `ClientJson.FromJson`:

| payload | bytes | mean | allocated |
| --- | ---: | ---: | ---: |
| `updateUserStatus` | 105 | 207 ns | 56 B |
| `updateFile` | 482 | 759 ns | 184 B |
| `updateNewMessage` | 1,401 | 2,773 ns | 1,088 B |
| `messages` ×50 | 68,200 | 126,716 ns | 54,376 B |

A whole message with text content, entities and sender parses in 2.8 µs here — but **7.4 µs on
.NET Native**, and a 50-message page costs 443 µs rather than 127 µs. Even so a 10k-update cold sync
spends on the order of 30–70 ms in JSON, so the parse is not where the app is losing its time.
Allocation is the number worth watching — 1 KB per message, 54 KB per page — and no change of wire
format moves it.

### The NUL scan is ~10% of the parse, and it's free to delete

`Client.Receive` finds the payload length with `while (*end != 0) end++;` over the whole payload.

| size | scan | `IndexOf(0)` | length from the ABI |
| ---: | ---: | ---: | ---: |
| 482 | 88.7 ns | 7.7 ns | ~0 |
| 1,401 | 247.3 ns | 25.0 ns | ~0 |
| 68,200 | 11,834.7 ns | 1,277.1 ns | ~0 |

Consistently 9–12% of total parse time. `json_receive` has the length already; the patched
`td_receive` signature just doesn't return it.

The `IndexOf` column does **not** hold on .NET Native, where it is slower than the naive loop — see
below. Returning the length is the fix; swapping in `IndexOf` is not.

### Field dispatch: real but small, and it depends on the type

CRC32 of the property name into `switch (hash)`, against `switch (name.Length)` then an exact
`SequenceEqual`:

| type | fields | CRC32 | length + compare |
| --- | ---: | ---: | ---: |
| `localFile` | 8 | 314.9 ns | 251.6 ns (−20%) |
| `message` | 42 | 2,236.1 ns | 2,211.4 ns (−1%, noise) |

Worth having on small flat objects, worth nothing on a big one — `message` is dominated by its
nested objects and value parsing, not by finding which field it's looking at. It also removes the
possibility of an unknown field colliding with a known one's hash, which is the real argument for
it. `AltParsers.g.cs` holds the candidate, emitted by `scratchpad/GenAltParsers.cs`; the change
belongs in `SchemaGenerator.WriteToJson` if it's taken.

**Not taken for the `FromJson_*` parsers, and the reason is not the size of the win.** Measured a
third time on .NET Native (2026-08-14): `message` 7.74 → 7.20 µs (−7%), `localFile` 1.08 µs →
969.8 ns (−10%) — small, consistent, in the same direction everywhere. But the pointer parsers
dispatch this way already, and they are what `Client.Receive` runs, so the payloads where it would
pay no longer go through `FromJson_*` at all. What is left there is `Client.Execute` and the two
instant view entry points, none of them hot. Changing it would also cost the check that makes the
generator safe to touch — that its output with the pointer mode off is byte for byte what shipped —
for a few percent of a path that isn't measured in this app's frame times.

### Pooled exact-size arrays instead of a growing List: rejected

| | mean | allocated |
| --- | ---: | ---: |
| `List<T>` growing from 4 | 115.7 µs | 53.07 KB |
| pooled scratch + exact array | 121.6 µs | 52.37 KB |

5% slower for 1.3% less allocated. The 50 parsed `Message` objects dwarf the list's intermediate
backing arrays. `IList<T>` → `T[]` is still worth doing for retained footprint and for reading, but
not on parse-throughput grounds.

### Round trips through the real tdjson.dll

TDLib's own offline test methods (`testCallEmpty`, `testCallString`, `testCallBytes`,
`testCallVectorInt`, `testCallVectorStringObject`) echo their argument back, need no account and no
network, and so measure **both** halves of the JSON path — C# serialize, TDLib parse, TDLib
serialize, C# parse — with no app involved. When a binary client exists, the same methods compare
the two formats with nothing else changed.

| | mean | allocated |
| --- | ---: | ---: |
| `testCallEmpty` | 32.6 µs | 48 B |
| `testCallString` 1 KB | 38.5 µs | 2,120 B |
| `testCallBytes` 1 KB | 45.3 µs | 1,096 B |
| `testCallString` 64 KB | 346.6 µs | 131,144 B |
| `testCallBytes` 64 KB | 444.2 µs | 65,608 B |
| `testCallVectorInt` ×1000 | 243.1 µs | 8,504 B |
| `testCallVectorStringObject` ×1000 | 672.9 µs | 72,648 B |

`testCallEmpty` at 32.6 µs is the floor — that's TDLib's actor queue, not serialization, and it
dwarfs the 2.8 µs it takes to parse a whole message. For *requests*, the wire format is noise.
Updates don't pay it, since they're pushed rather than round-tripped.

Subtracting the floor: base64 costs about 98 µs per 64 KB round trip, ~28% on top of the same
payload as a string — that one is real and it's what binary TL would delete. 1000 ints cost ~210 µs
(~210 ns each, both directions) in pure number formatting and parsing. 1000 one-field objects cost
~640 µs, roughly 3× an int apiece, which is the per-object type-name emit and dispatch.

### The pointer tokeniser prototype

`Json\TdJsonReader.cs` — a pull reader over `td_receive`'s pointer, shaped like the slice of
`Utf8JsonReader` the generated parser uses, reusing `JsonTokenType` so generated code needs no
change beyond the reader type. Raw bytes for scanning; spans only handed to library helpers
(`SequenceEqual`, `Utf8Parser`), never indexed in a loop.

Same payloads, same work — read every token, compare each property name:

| | .NET Native | | desktop JIT | |
| --- | ---: | ---: | ---: | ---: |
| | `Utf8JsonReader` | `TdJsonReader` | `Utf8JsonReader` | `TdJsonReader` |
| `updateUserStatus` | 427.6 ns | **128.3 ns** (3.3×) | 102.7 ns | **74.4 ns** (1.4×) |
| `updateFile` | 2.05 µs | **562.3 ns** (3.6×) | 374.9 ns | **289.4 ns** (1.3×) |
| `updateNewMessage` | 5.39 µs | **1.47 µs** (3.7×) | 977.1 ns | **757.4 ns** (1.3×) |
| `messages` ×50 | 318.4 µs | **74.9 µs** (4.3×) | 47.35 µs | **40.02 µs** (1.2×) |

End to end on a real type — `localFile`, eight scalars, so it measures the reader rather than a
graph of allocations — building the same object:

| | .NET Native | desktop JIT |
| --- | ---: | ---: |
| `Utf8JsonReader` (generated) | 1.08 µs | 350.7 ns |
| `TdJsonReader` (pointer) | **360.2 ns** (3.0×) | **322.0 ns** (1.09×) |

Allocation is identical (71 B either way), so the parser stays copy- and allocation-free.

Faster on **both** hosts, which is what makes it worth starting before a runtime migration rather
than after. Tokenising is 60–70% of the current parse on .NET Native (5.39 µs of ~7.5 µs for
`updateNewMessage`, 318 µs of ~440 µs for a page), so the full parse should land around **2–2.5×**
once the generated parsers move over — better than the 1.5–1.8× estimated before measuring.

#### The generator emits both parsers

`SchemaGenerator` emits a second set, `FromPtr_*`, reading through `TdJsonReader` instead of
`Utf8JsonReader`. Both come from the same schema, so they cannot drift. Which set it emits is
`<TdParsers>` — `Reader`, `Pointer` or `Both`, plus `<CompilerVisibleProperty Include="TdParsers" />`
— and unset means `Reader`, whose output is byte for byte what it has always been. That diff is the
check that makes changing the generator safe without being able to build the app.

Field dispatch uses name length then an exact compare rather than CRC32: few enough fields per class
for that to be cheap, and an unknown field cannot collide with a known one. `@type` dispatch keeps
the CRC32 switch — up to eighty constructors is the wrong shape for a compare chain — using
`TdJsonReader.ValueCrc32`, which walks raw memory and must agree with the values the generator bakes
in.

Full parse on .NET Native, same payloads, same objects out:

| | `FromJson` | `FromPtr` | |
| --- | ---: | ---: | ---: |
| `updateUserStatus` | 682.2 ns | **335.6 ns** | 2.0× |
| `updateFile` | 2.78 µs | **1.12 µs** | 2.5× |
| `updateNewMessage` | 8.15 µs | **3.76 µs** | 2.2× |
| `messages` ×50 | 436.3 µs | **181.7 µs** | 2.4× |
| `updateOption` (escapes) | 1.41 µs | **493.2 ns** | 2.9× |

**2.0–2.9×**, matching the 2–2.5× predicted from the tokeniser numbers, with allocation identical to
the byte. On the desktop JIT the two are at parity (97.2 µs against 97.8 µs for a page).

`Validation` runs the same assertions over both readers' output on every host, and on the desktop
also compares the two object graphs field by field through reflection. Both report `validation ok`
including on .NET Native, so the pointer parsers agree with the netstandard2.0 System.Text.Json ones
across nested objects, vectors, abstract dispatch, escapes and unknown fields.

#### Files go back through the handler on both paths

`updateFile` and `file` are re-entered through `ClientResultHandler` rather than parsed inline, so a
file is read into the instance the app already holds — they arrive by the hundred on every history
page, nearly always for an id already seen, and that is the one place object identity pays. The
interface now carries both overloads of `ParseUpdateFile`/`ParseFile`, and `ClientService`
implements the pointer pair beside the reader pair; the dedupe, the first-sight existence check and
the `UpdateFile` dispatch are shared between them.

It costs nothing measurable. On .NET Native after the change, `updateFile` parses in 2.70 µs through
`Utf8JsonReader` against **1.09 µs** through the pointer reader, and a 50-message page in 444.7 µs
against **182.8 µs** — the same 2.4–2.5× as before the handler round trip existed on this path.

The interface lives in the app, so the reader moved with it: `Telegram/Td/TdJsonReader.cs` and
`Telegram/Td/PtrClientJson.cs` are app files now, linked into all three benchmark hosts rather than
the other way round. Nothing in `PtrClientJson.cs` refers to generated code — the one member that
did, the `FromPtr(byte*, int)` entry point, is emitted beside `DoFromPtr` instead — so the app takes
both files in any mode, whether or not anything calls them.

Both files do get compiled by the .NET Native host, which is the same toolchain the app uses.
`ClientService`'s half does not: nothing here builds `Telegram.csproj`, so the pointer
`ParseFile`/`ParseUpdateFile` there are checked by reading them against the `Utf8JsonReader` pair
and against what the generator emits for every other type, not by a compiler.

#### The receive path, and one switch that picks a parser

`<TdParsers>` in `Telegram.csproj` decides which parser the app is built with, and it is a choice
of one:

| | generator emits | constants | `Client.Receive` parses |
| --- | --- | --- | --- |
| `Reader` | `FromJson_*` | `TD_READER_PARSER` | a copy of the payload, through `Utf8JsonReader` |
| `Pointer` | `FromPtr_*` | `TD_POINTER_PARSER` | `td_receive`'s buffer, through `TdJsonReader` |
| `Both` | both | both | the pointer path |

The property is what the generator reads; the constants are what the hand-written code reads, so
`ClientJson`, `Client` and `ClientService` compile exactly the half that was generated. Nothing is
deleted to switch — the `Utf8JsonReader` code is still there under `#if`, which is the point: this
is a build mode, not a migration you cannot walk back.

`Both` exists for `Telegram.Benchmarks`, whose entire argument is the two parsers racing over one
corpus and agreeing field for field. The app has no reason to carry both — 211,651 generated lines
against 167,143 — and it is set to `Both` at the moment only because the pointer path has yet to be
built and run even once.

All three modes compile: `Reader`'s generated output is byte for byte what shipped, and a throwaway
project outside the repo builds `Client.cs`, `ClientJson.cs`, `PtrClientJson.cs`, `TdJsonReader.cs`
and the generated file in each of the three.

On the pointer path `Receive` no longer copies into `_buffer` and no longer scans for the
terminator. The scan was 9–12% of the old parse and would have been a fifth of this one; it goes
because `TdJsonReader` needs no length when the buffer is NUL-terminated, which is what
`TdJsonReader.NulTerminated` says at the call site. What that gives up is the per-token
`_index <= _length` test, which only ever catches a literal truncated near the end of the buffer —
TDLib serializes whole objects, so a payload cannot end mid-literal.

**Not built.** `Telegram.csproj` is not built here, so this is the step where the app has to be
compiled and run before it is believed. `Client.cs` and the two `Td/` files do compile both ways —
a throwaway project outside the repo compiles them with the switch on and off — but `ClientService`
and everything downstream of `Client.Receive` have only been read.

#### In the app, at last: startup on .NET Native Release

Diagnostics ▸ TDLib JSON, one cold start, pointer parsers:

| | |
| --- | ---: |
| updates | 15,431 |
| bytes | 25.0 MB (mean 1,699 B) |
| parsing | 0.165 s — 10.7 µs each, 151.2 MB/s |
| file handling | 0.086 s — 34% of the parse |
| ...of which existence syscalls | 1,152 checks, 0.085 s, 74 µs each |
| total | 0.25 s, 3.25% of an 8 s startup |

Three things fall out of it.

**The parse runs at 40% of its benchmark rate.** 151 MB/s here against 377 MB/s for a 1,401 B
payload in the corpus loop. Same parser, same toolchain — the difference is the setting: a growing
heap, a GC that runs, cold code, and a real mix of payloads rather than one payload over and over.
Nothing here is wrong, but the corpus number is a ceiling, and it is 2.4× above what the app sees.

**File existence checks did not get faster, so they got bigger.** 109 µs in Debug against 74 µs
here — a third off, where the parse around them dropped 3.4×. That is the signature of I/O and an
AppContainer access check rather than codegen, which settles what to do about them: they cannot be
made cheaper, only rarer or asynchronous. They were 11% of the TDLib thread's parse work in Debug
and are **34%** of it here, and they will keep growing as a share as the parser improves.

**0.25 s of TDLib-thread time during an 8 s startup**, a third of it syscalls. Whether that is worth
anything depends on what waits on the update pipeline before the chat list is usable.

The obvious experiment now that both parsers are a build switch: run the same startup with
`<TdParsers>Reader</TdParsers>` and read the same page. That measures the pointer reader's worth
*in the app*, which no number in this file does — everything above is a corpus in a loop.

**What that A/B leaves out.** `TdThroughput.Begin` is called after the copy and the terminator scan
on the `Reader` path, so both modes time the parse alone. The copy and the scan are work the pointer
path deleted outright — 9–12% of the old parse by the corpus — and neither run will show it. The
comparison is therefore parser against parser, and understates what the change did to `Receive`.

#### After the file checks moved off the thread

Same page, next startup — 11,249 updates, 22.7 MB, mean 2,115 B:

| | before | after |
| --- | ---: | ---: |
| file handling | 0.086 s, 34% of the parse | **0.007 s, 4%** |
| existence checks | 1,152, 0.085 s, on the TDLib thread | 1,080, 0.094 s, **off it** |
| per update | 16.3 µs | 15.4 µs |

The checks cost the same — they are the same syscalls — they are simply no longer in the pipeline.
What is left of file handling is the dictionary, the publish and the enqueue: 6.7 ms across 11,249
updates, 0.6 µs each. Throughput reads lower here (136.4 MB/s against 151.2) but the payloads are
24% larger, so the two startups are not directly comparable; only the within-run figures are.

#### What `Pointer` mode compiles out

Everything the `Utf8JsonReader` parsers need and nothing else:

- the generated `DoFromJson` and every `FromJson_*` — 44,508 lines, the bulk of it;
- `ClientJson.FromJson(ReadOnlySpan<byte>)`, `FromJson<T>`, `ParseObject`, the `FromHandler` and
  `ParseHandler` delegates, and the reading half of `Utf8JsonExtensions` — `ReadStartObject`,
  `GetInt64String`, the `Get*Array` family. The writing half is unconditional: requests are
  serialized with `Utf8JsonWriter` whichever parser was generated, so System.Text.Json does not go
  anywhere. Only reading changes.
- `ClientResultHandler`'s two `Utf8JsonReader` overloads and `ClientService`'s implementations of
  them, ~110 lines;
- the `#else` in `Client.Receive`/`Client.Execute`, and `_buffer` with it.

`ClientJson.FromJson(string)` is what made this possible: `RichHtml` and `RichEditorCommands` each
encoded their own span into `FromJson`, and now go through one entry point that follows the switch
like everything else.

Generated size by mode: `Reader` 149,712 lines, `Pointer` 167,143, `Both` 211,651. Pointer-only
lands ~12% above where the file started rather than below it — the pointer parsers are ~62k lines to
the reader parsers' ~44.5k, because name-length grouping costs more source than a hash switch. If
that matters, the TODO in `SchemaGenerator` about emitting parsers only for types that can actually
be received is worth several times more than the choice of reader.

Two things stay in every mode. `crc32_table`, one of the four, because `TdJsonReader.ValueCrc32`
hashes `@type` with it. And **both emitters in the generator**, which is what `Both` is for:
delete the reference implementation and you delete the cross-check that says the pointer parsers
are right.

#### Hardening pass: free on .NET Native, 43% on the JIT

Bounds-checking every advance costs **43% on the desktop JIT** and **nothing on .NET Native**. Both
measured three ways in a single process, so machine load cancels out.

.NET Native, idle machine — the toolchain that ships:

| | `Utf8JsonReader` | `TdJsonReader` | unchecked | speedup |
| --- | ---: | ---: | ---: | ---: |
| `updateUserStatus` | 473.4 ns | **154.8 ns** | 152.0 ns | 3.1× |
| `updateFile` | 2.09 µs | **611.1 ns** | 632.4 ns | 3.4× |
| `updateNewMessage` | 5.58 µs | **1.71 µs** | 1.64 µs | 3.3× |
| `messages` ×50 | 300.0 µs | **81.3 µs** | 81.1 µs | 3.7× |
| `updateOption` (escapes) | 651.1 ns | **190.7 ns** | 181.5 ns | 3.4× |

Checked against unchecked is 0.3% on the 68 KB payload, and on `updateFile` the checked version is
nominally *faster* — i.e. noise. Safety is free here.

Desktop JIT, same three-way in one run:

| `messages` ×50 | |
| --- | ---: |
| `Utf8JsonReader` | 140.99 µs |
| `TdJsonReader`, bounds-checked | 214.40 µs |
| `TdJsonReader`, checks stripped | 149.73 µs |

The checks each end in `return Fail()`, and a call inside `ReadString`/`ReadNumber`/`Read` is enough
to stop RyuJIT inlining them into the scan loop. UTC's whole-program inliner doesn't care. So the
type's original comment — "one comparison per token" — was wrong on the JIT and right on .NET Native.

#### The sentinel is in; the JIT gap is not explained

`td_receive` returns a NUL-terminated buffer, so `TdJsonReader` now uses the terminator as its
sentinel: the scan loops test only for content, and `_index <= _length` is checked once per token
rather than once per byte. `Fixtures.Load` appends a terminator and `GuardedBuffer.Place` writes one
inside the committed region, so the guard page still sits immediately after it.

On .NET Native it is a small win — **4.1×** against `Utf8JsonReader` on the 68 KB payload, up from
3.7×, and 2.9× end to end on `localFile`.

On the desktop JIT it did **not** recover the 43%, and the reader is still ~1.65× slower than
`Utf8JsonReader` there against 0.85× before the hardening pass. Two hypotheses have been measured
and refuted:

- *the bounds checks* — removing them left the reader still slower than the original relative to its
  own `Utf8JsonReader` baseline, and the sentinel that removes them entirely changed nothing;
- *`Fail()` calls inhibiting inlining of the scan methods* — marking it `AggressiveInlining` moved
  nothing.

So something else introduced in hardening costs roughly 2× on RyuJIT and nothing on UTC. The way to
find it is a side-by-side of the pre-hardening reader against the current one in a single process,
the way the checked/unchecked comparison was done — comparing across runs is what produced both
wrong answers. **It does not affect the toolchain the app ships on**, so it is parked rather than
chased.

End to end on a real type, .NET Native — `localFile`, same object out, same 71 B allocated:

| | |
| --- | ---: |
| `Utf8JsonReader` (generated) | 991.3 ns |
| `TdJsonReader` (pointer) | **389.8 ns** (2.5×) |

Tokenising is 76% of the full `updateNewMessage` parse on .NET Native (5.58 µs of 7.02 µs) and 75%
of a page (300 µs of 399 µs), so the generated parsers moving over should land around **2–2.5×** on
the whole path.

**Memory safety.** `StaysInsideItsBuffer` runs every prefix of every payload — 70,512 of them —
with the byte after the last one on a `PAGE_NOACCESS` guard page, so an overrun access-violates
instead of quietly reading whatever came next. All pass.

The test was itself verified to fail: relaxing `ReadNumber`'s loop to `_index <= _length` produces
an access violation with the stack pointing at `ReadNumber`. Worth knowing that removing the
*literal* bounds check does **not** trip it — that check only advances the index and never
dereferences, so it guards against accepting a truncated literal, not against an out-of-bounds read.

**Correctness.** `Program.cs` requires the tokeniser to agree with `Utf8JsonReader` token for token
*and* string for string over every corpus payload, and to produce a `LocalFile` identical to the
generated parser's. `Corpus\synthetic-escapes.jsonl` exists for this: `\"`, `\\`, `\/`, `\n`, `\t`,
a `` control escape, `é`, an astral surrogate pair, and literal multibyte UTF-8. Both
checks pass.

**All three hosts validate.** `Validation.cs` is host-independent and runs on the desktop console,
the NativeAOT UWP host and the .NET Native one — the last being the one that matters, since it is
the only host that resolves System.Text.Json's netstandard2.0 asset. Agreeing with the `net10.0`
reader would have proved nothing about the reader the app actually parses against. All three report
`validation ok`.

Two things are deliberately desktop-only: the guard-page sweep, because `VirtualAlloc` isn't in the
app container API set, and the reflection-based whole-object comparison, because .NET Native only
keeps the metadata it is told to keep. The shared checks use explicit field comparisons instead.

### Why .NET Native is 3× slower: `Span<T>` is not the runtime's span

UWP resolves **`lib/netstandard2.0/System.Memory.dll`** as well as `lib/netstandard2.0/System.Text.Json.dll`
(verified in `Telegram.Benchmarks.NetNative\obj\project.assets.json`), so `Span<T>` comes from the
package, not from the runtime. Same 4 KB traversal, four ways:

| | .NET Native | desktop JIT |
| --- | ---: | ---: |
| `byte[]` index | 629.6 ns | 1.22 µs |
| `byte*` pointer | 616.6 ns | 1.26 µs |
| `ReadOnlySpan<byte>` index | **7.93 µs** | 1.08 µs |
| span + `MemoryMarshal.GetReference` + `Unsafe.Add` | **5.44 µs** | — |
| `span.Slice()` ×256 | 1.73 µs | 116 ns |

Element access through a span is **12.6× slower than the same loop over `byte[]`** on the shipping
toolchain — where on the JIT the span is the *fastest* of the three. Arrays and raw pointers are
indistinguishable from each other and, in absolute terms, quicker than the JIT manages.

Taking the ref up front and walking it with `Unsafe.Add` recovers only 31% and is still 8.6× off
`byte[]`, so this is not a bounds check that can be hoisted away — it's the portable span's
representation. **On .NET Native you cannot fix span element access by accessing it differently;
you can only stop using spans in hot loops.**

Corelib helpers, by contrast, are merely slower, not pathological (.NET Native ÷ desktop):

| | ratio |
| --- | ---: |
| `Utf8Parser.TryParse` int64 | 1.5× |
| `Convert.FromBase64String` 1 KB | 1.3× |
| `Encoding.UTF8.GetString` 59 B | 2.5× |
| `MemoryExtensions.IndexOf` 4 KB | 2.7× |
| `MemoryExtensions.SequenceEqual` 15 B | 3.6× |
| **`Array.IndexOf` 4 KB** | **32.6×** |

`Array.IndexOf<byte>` is the one to never call on this framework — that, not span search, is what
the earlier "IndexOf is a trap" note was really measuring. `MemoryExtensions.IndexOf` is fine.

### Reading the payload td_receive hands back

Tokeniser-shaped workload (find every quote) over a native buffer, three ways:

| | .NET Native | .NET 10 AOT |
| --- | ---: | ---: |
| `byte*` direct, 1,401 B | **632.7 ns** | 660.9 ns |
| span over native, 1,401 B | 2.68 µs | 907.2 ns |
| copy then `byte[]`, 1,401 B | 683.0 ns | 792.4 ns |
| `byte*` direct, 68,200 B | **31.54 µs** | 28.66 µs |
| span over native, 68,200 B | 128.69 µs | 45.03 µs |
| copy then `byte[]`, 68,200 B | 32.35 µs | 39.62 µs |

Reading the pointer directly beats copying into a managed array by 7% at 1.4 KB and 2.5% at 68 KB —
the copy is genuinely cheap, ~50 ns and ~0.8 µs respectively. Wrapping the pointer in a span costs
**4.2×**. That is the whole explanation for why removing the copy from `Client.Receive` made things
slower: it traded a 50 ns memcpy for a 4× per-byte penalty.

A `byte*` tokeniser can therefore skip the copy outright. Note the refinement this adds to the span
result above: *indexing* a span in your own loop costs ~10×, but *handing* a span to a library
helper is only 1.5–3.6× (`Utf8Parser` parses a 19-digit int64 in 17.7 ns, well under what per-byte
span indexing would imply). So the design is to walk raw bytes and pass spans to `Utf8Parser`,
`SequenceEqual` and friends — not to avoid spans entirely.

### WinRT interop: MCG vs CsWinRT

.NET Native marshals through MCG's compile-time stubs (`Telegram.McgInterop` in the app's obj tree);
.NET 9+ uses CsWinRT projections. Thread-agile types, so this is the interop layer without XAML on
top:

| | .NET Native (MCG) | .NET 10 AOT (CsWinRT) |
| --- | ---: | ---: |
| property get, int | 13.6 ns | **8.9 ns** |
| property get, string | 195.8 ns | **156.0 ns** |
| method call, void | 68.6 ns | **59.5 ns** |
| map read, boxed int | 223.3 ns | **84.0 ns** |
| map write, boxed int | 1.08 µs | **666.7 ns** |

**CsWinRT is faster on every operation measured**, by 1.15× to 2.7×. The hypothesis that it's where
the new stack loses does not survive contact with the numbers.

Two caveats. The `new Calendar()` row (76.6 µs vs 64.9 µs) measures Calendar loading globalization
data, not activation plumbing — ignore it, and replace it with something cheaper to construct before
quoting it. And the app's real COM traffic is XAML `DependencyProperty` access on the UI thread,
which stacks XAML's own cost on top of this and wants a UI-thread harness to measure.

#### CsWinRT 3.0 preview does not build a UWP XAML app yet

The numbers above are CsWinRT 2.x, because 3.0 could not be made to work. Recorded so nobody
repeats it — retry when a later preview lands.

`3.0.0-preview.260319.2` needs both halves: the package *and* the matching SDK targeting package,
selected by an OS TFM with a `.1` suffix (`net10.0-windows10.0.26100.1` for 3.0,
`...26100.0` stays on 2.x), with `<WindowsSdkPackageVersion>10.0.26100.85-preview</WindowsSdkPackageVersion>`.
Referencing the package alone fails at compile: the 3.0 generator emits `WinRTExposedTypeAttribute`
and friends that the 2.x `WinRT.Runtime` doesn't define. With both halves the projections generate
fine — and then:

```
RUNCSWINRTINTEROPGENERATOR : error CSWINRTINTEROPGEN0055: Failed to generate the type signature
for type 'Windows.UI.Xaml.Controls.DatePickerValueChangedEventArgs'. Outer exception:
'CSWINRTINTEROPGEN0011': 'Failed to generate marshalling code for delegate type
'EventHandler`1<Windows.UI.Xaml.Controls.DatePickerValueChangedEventArgs>'.'
```

`-p:CsWinRTGenerateInteropAssembly2=false` gets past it (the property has to be a global property;
set in the csproj it's overwritten by the package targets), and the build then succeeds — but
publishing fails at the AOT step with `Failed to load assembly 'WinRT.Sdk.Xaml.Projection'`,
because ILC wants the projection assembly that switch just suppressed. So the two states are
"crashes generating XAML marshalling" and "can't link without it".

Consistent with the release notes, which don't mention UWP. Also worth knowing: 3.0 drops
netstandard2.0 support and removes `As<I>()`, `FromAbi(nint)` and `FromManaged(object)`, and defines
a `CSWINRT3_0` constant for conditional compilation.

### .NET Native — the toolchain the app actually ships

`Telegram.Benchmarks.NetNative`, a legacy UWP project, `UseDotNetNativeToolchain`, .NET Native
4.6.29511.0. Same `Suite.cs`. This is the only host that also gets System.Text.Json's
`netstandard2.0` asset, which is what the app really runs.

| | .NET Native | net10.0 JIT (BDN) | ratio |
| --- | ---: | ---: | ---: |
| `updateUserStatus` | 612.5 ns | 207.1 ns | 3.0× |
| `updateFile` | 2.60 µs | 759.4 ns | 3.4× |
| `updateNewMessage` | 7.36 µs | 2.77 µs | 2.7× |
| `messages` ×50 | 442.6 µs | 126.7 µs | 3.5× |

**The parse is about 3× slower than the desktop numbers suggested**, consistently across payload
sizes. Some of that is codegen and some is the netstandard2.0 System.Text.Json; this host can't
separate them, but it settles the question of which number to plan against. A 50-message page costs
443 µs, not 127 µs.

And a straight reversal:

| | scan for NUL | `IndexOf` |
| --- | ---: | ---: |
| 482 B | 135.6 ns | 229.7 ns |
| 1,401 B | 346.2 ns | 642.3 ns |
| 68,200 B | 15.49 µs | 28.53 µs |

`IndexOf` is **1.7–1.9× slower than the naive byte loop** here, where on the JIT it was 5–10×
faster: .NET Native doesn't give `Array.IndexOf<byte>` the vectorised path. Getting the length from
the ABI is still the right fix — it deletes the work rather than speeding it up — but "just use
IndexOf" would have made things worse, and nothing but this host would have caught that.

Field dispatch here: `message` 7.83 → 7.56 µs (−3%), `localFile` 1.19 → 1.09 µs (−8%). Same
direction as everywhere else, smaller, and inside the noise band.

Round trips: base64 costs 147 µs per 64 KB (98 µs desktop, 125 µs UWP AOT) — consistent.

The allocation column is meaningless on this host: `GC.GetAllocatedBytesForCurrentThread` doesn't
exist in the UWP framework, so `Harness` falls back to a heap-size delta. Timings are the point here.

### UWP, NativeAOT, three runs of the same binary

| | run 1 | run 2 | run 3 |
| --- | ---: | ---: | ---: |
| `updateNewMessage` | 2.86 µs | 3.78 µs | 5.17 µs |
| `messages` ×50 | 132.4 µs | 198.1 µs | 267.6 µs |
| `message` crc32 → length+compare | −14% | **+17%** | −8% |
| `localFile` crc32 → length+compare | −21% | −18% | −22% |
| scan for NUL vs `IndexOf` (68 KB) | 4.6× | 4.8× | 4.5× |

Run 1 was on an idle machine and lands within 4% of the desktop JIT for the same work — **the parse
path AOTs well**, which is the useful headline. Runs 2 and 3 were on a loaded one and drift by up to
1.8×, so absolute numbers across runs mean nothing right now; only comparisons made *within* a run
do.

Read that way, two results are solid because they hold in every run on both runtimes: the NUL scan
is 4–5× slower than a vectorised search, and the dispatch change is worth ~20% on a small flat
object. The `message` dispatch comparison is **not resolved** — it changes sign between runs, so the
effect is smaller than this machine's noise. That matches the desktop BenchmarkDotNet result (−1%,
within its error bars) rather than contradicting it.

Round trips inside the app container reproduce the base64 gap: 407.8 µs for a 64 KB string against
533.1 µs for the same payload as bytes, +125 µs, against +98 µs on desktop.

## Where this stands

Everything below is committed on `develop`, and the app runs on the pointer path: `Client.Receive`
and `Client.Execute` parse straight off TDLib's buffer. **Built and run, Debug and .NET Native
Release**, by Fela — nothing here can build `Telegram.csproj`, so that is the only evidence any of
it works, and the startup numbers above are what it produced.

`<TdParsers>` picks the parser, one or the other, and `Reader` puts every part of this back the way
it was in a single edit — nothing was deleted to make room. It is set to `Both` today, which is a
holding position rather than an end state: the app has no use for two parsers, and `Both` exists so
the benchmark can race them.

**Done.** A pointer-based reader (`Telegram/Td/TdJsonReader.cs`) and a generator that emits parsers
against it, worth **2.0–2.9× on the full parse on .NET Native** with identical allocation. Three
hosts running one suite. `SchemaGenerator` rewritten from spike to something with diagnostics. Files
route through `ClientResultHandler` on both paths. The receive path reads TDLib's buffer directly:
no copy, no terminator scan. A build mode that compiles one parser or the other, verified in all
three settings. And the app measures itself: **0.25 s of TDLib-thread parsing across a 15,431-update
startup, 151 MB/s, a third of it file existence syscalls.**

**Next:**

1. The same startup with `<TdParsers>Reader</TdParsers>`, which is the A/B the switch exists for and
   the only measurement that says what the pointer reader is worth in the app rather than in a loop.
   Extrapolating the corpus ratio, parsing should go from 0.165 s to something near 0.4 s — but that
   is an extrapolation, and one run replaces it with a fact.
2. ~~Defer the file existence checks off the TDLib thread~~ — done. `ClientService.VerifyFileExists`
   queues the path and a single drain does the syscalls, so the same files are still checked and
   the same `DeleteFile` still goes out, just not mid-parse. Not a global check, either: `local.path`
   can point outside TDLib's cache, at an upload's source or somewhere a user has moved a file to,
   so the per-file question was the right one and only the thread was wrong. `NativeFile.Exists`
   also replaces the C++/WinRT hop with the P/Invoke it was wrapping. **Worth re-reading the page
   after this**: file handling should fall to near nothing — it was 0.086 s of which 0.085 s was
   these calls — and the checks now report their own time, off the parse entirely.
3. Then `<TdParsers>Pointer</TdParsers>`, and the `Utf8JsonReader` set stops being compiled at all —
   44,508 generated lines and ~500 hand-written ones. One word, and the only step left that the
   benchmark cannot check for you is `ClientService`, whose `#if` blocks nothing here compiles.

**Independent of that, still open:**

- `ParseObject` in `ClientJson.cs` skips an unknown *object* field but not an unknown *array*, so
  one would truncate the rest of the object. Latent — every field the generator drops today is a
  scalar — but it fires on the first bots-only vector TDLib adds, or on version skew. One line. The
  pointer parsers already handle it.
- Omitting default-valued scalars in TDLib's `to_json` (~6 lines in `tl_json_converter.cpp`) would
  delete ~16 `false` booleans per message from both sides. Bools, ints and strings only — omitting
  an empty vector would turn an `IList<T>` into null.
- ~~Returning the payload length from `td_receive`~~ — settled by the sentinel instead. The reader
  scans to `\0` and never needs the length, so the strlen in `Client.Receive` is gone without
  patching TDLib. Worth revisiting only if the missing `_index <= _length` bound is ever wanted
  back.
- Emitting `FromJson` only for types that can be received — see the TODO in `SchemaGenerator`,
  which records why it stopped working rather than just the intent.

## Open questions, and traps worth not re-learning

- **The jank harness was never built.** Everything measured here is throughput. The .NET 10
  migration question is about smoothness and deadlocks, which none of these numbers speak to. A
  second thread ticking at 60 Hz while the parse workload runs, reporting p99 lateness, would say
  something real and runs on all three hosts.
- **The JIT regression is unexplained.** `TdJsonReader` is ~1.65× slower than `Utf8JsonReader` on
  the desktop JIT, against 0.85× before the hardening pass. Two hypotheses measured and refuted.
  Doesn't affect the shipping toolchain.
- **Compare within a run, never across runs.** This machine varies up to 1.8×, and every wrong
  conclusion in this file came from comparing two numbers taken at different times. The
  checked-vs-unchecked answer only fell out when both ran in one process.
- **The hosts disagree on ranking, not just scale.** `Array.IndexOf<byte>` is 32× slower on .NET
  Native than on the JIT while `MemoryExtensions.IndexOf` is only 2.7×. Nothing measured on the
  desktop should be acted on until .NET Native agrees.
- **.NET Native's 3× is still confounded** between codegen and the netstandard2.0 System.Text.Json.
  Referencing the netstandard2.0 STJ DLL from the NuGet cache in the desktop project would separate
  them — and the span result makes the library the likely answer.
- **Allocation on .NET Native reads `-1`** when a background collection lands mid-window.
- **`new Calendar()`** in the interop group measures Calendar loading globalization data, not
  activation. Replace it before quoting that row.
- **A live TDLib client pollutes everything measured after it** — its background threads land in
  every subsequent row. That is why round trips initialise lazily and run last.

