# WinRTExposure

Finds bindings where the type CsWinRT can see is not the type that arrives.

An object crossing the WinRT ABI needs a CCW vtable for its **exact runtime type**. CsWinRT's
generator emits one for every instantiation it can see converted in source. A mismatch only matters
where the value actually crosses — so this reports a member only when it is **both** mis-typed and
**bound to a sink**:

- **non-concrete** — declared as an interface or abstract type, so nothing in source names the
  runtime type at all (`IList<StorageChartItem>` bound to `ItemsSource`).
- **mismatch** — declared as one generic type and constructed as another
  (`ObservableCollection<Passkey>` assigned `new IncrementalCollection<Passkey>()`).

```
dotnet run -c Release --project Tools\WinRTExposure -- Telegram
dotnet run -c Release --project Tools\WinRTExposure -- Telegram -all   # + every mismatch, sink or not
```

## What counts as a sink

Every `{x:Bind}`/`{Binding}` in XAML, **except** a binding to a plain CLR property declared on one
of our own controls. `x:Bind` sets those with a direct managed call and nothing reaches the ABI —
`StorageChart.Items` is a plain `IList<StorageChartItem>` property backed by a field, and binding to
it is free. A `DependencyProperty` is a sink (`SetValue` takes `IInspectable`), and so is any
property the repo does not declare, which means it belongs to the framework.

A property inherited from a repo base class is not resolved, so it stays a sink — the error is
toward reporting, which is the right direction.

The C# side (`control.ItemsSource = x`) is still a curated list of property names, because deciding
whether `foo.Bar = x` crosses the ABI needs `foo`'s type, and this tool does not bind. XAML is where
the coverage is real.

## Reading the output

The coverage column says what already covers the **runtime** type:

| mark | meaning |
| --- | --- |
| `vtable` | the generated `WinRTGlobalVtableLookup.g.cs` has a key for it — covered |
| `manifest` | named in `CsWinRT.cs`/`CsWinRT.Vectors.cs` but no vtable key — the build may be stale |
| `MISSING` | neither — investigate |
| `?` | the runtime type is not constructed with `new` in the declaring type, so it is unknown |

The vtable column reads the newest `WinRTGlobalVtableLookup.g.cs` under the source root, so **build
first**; without one it degrades to what the manifest names. The manifest is parsed as C# syntax with
`NET9_0_OR_GREATER` defined, so commented-out entries correctly read as absent.

## What it does and does not see

Roslyn syntax, no binding. Exact for an explicit declaration paired with an explicit `new`, and blind
to anything needing a symbol: a factory return type, `var`, a cast. `{x:Bind ViewModel.X}` resolves
through each page's `ViewModel` property; code-behind `SetBinding` is not covered.

The compilation-bound version of this question is `WinRTExposedTypeAnalyzer` (TG1001/TG1002) in
`Telegram.Generators.WinRT`, which runs on every Release build and sees real symbols.

## Validation

The detector is checked against a known answer rather than trusted. Extract the tree at a commit
where the defects existed and run against it:

```powershell
git archive HEAD Telegram | tar -x -C $env:TEMP\head-tree
dotnet run -c Release --project Tools\WinRTExposure -- $env:TEMP\head-tree\Telegram
```

At the commit before the 2026-08-29 fixes this reports 5 hits — `ChatStoriesViewModel.SelectedItems`,
`SettingsBlockedChatsViewModel.Items`, `SettingsPasskeysViewModel.Items`,
`SettingsStorageViewModel.ItemsView`, `SettingsWebSessionsViewModel.Items` — and 0 against the tree
with those fixed. A zero from an unexercised detector is worth nothing; this is how you tell them
apart.
