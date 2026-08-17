# Audit: classic bindings under CsWinRT and NativeAOT

Written 2026-08-17, against the working tree at `4cbb362fc`. Closes out the Phase 4 item in
`net10-port-todo.md` that read *"`{Binding}` is where the runtime differences will show"* — it was
the analyzer's stated blind spot, since a `{Binding}` in markup has no C# anywhere for
`TG1001`/`TG1002` to look at.

Two surfaces, not one, and the second is the one the todo did not mention:

- **markup** — `{Binding}` in XAML, plus the reflective attributes `DisplayMemberPath`,
  `SelectedValuePath` and `CollectionViewSource.ItemsPath`;
- **code-behind** — a `Binding` constructed in C# and applied with `SetBinding`, and the same
  reflective paths assigned from code.

Both resolve a property name at runtime, which is the thing NativeAOT cannot do without help. Three
confirmed defects fell out, one of them on the second surface.

Every classification below is checked against generated output in
`obj\modern\x64\Release\net10.0-windows10.0.26100.0\win-x64\`, not inferred.

## The numbers

| | |
|---|---|
| `{Binding}` occurrences in tracked XAML | **108** across **26** of 475 files |
| bindings built in C# (`SetBinding` + `new Binding`) | **3** |
| reflective paths set in C# (`ItemsPath`) | **1** |
| `{x:Bind}` | 2038 |
| `{TemplateBinding}` | 12492 |
| types carrying `[GeneratedBindableCustomProperty]` | 5 |
| member accessors in `XamlTypeInfo.g.cs` | 1436, under 1291 `Type.Member` keys |

The todo's figure of "180 occurrences across 32 of 481 files" was counting the `obj\` and `bin\`
copies as well; 108/26 is the tracked-source number.

`{TemplateBinding}` is not in scope: it binds dependency property to dependency property through
the property system and never resolves a name at runtime. Neither is `{x:Bind}`, which the XAML
compiler turns into direct C#.

## What actually resolves a classic binding — two mechanisms, and they are disjoint here

This is the finding that made the audit tractable, and it is narrower than the todo assumed.

1. **`XamlTypeInfo.g.cs`.** The XAML compiler emits a real getter/setter pair for every member it
   sees a binding target, keyed as `"Telegram.Controls.HeaderedControl.Header"`. This is generated
   for *both* projects and needs no attribute and no registration. It covers any type the compiler
   can see from markup.
2. **`[GeneratedBindableCustomProperty]`** → `WinRTCustomBindableProperties.g.cs`, an
   `IBindableCustomPropertyImplementation.GetProperty(string)` per type. This is for types the XAML
   compiler *cannot* see, because they only ever turn up as a runtime `DataContext` — group objects
   and item models.

In this app the two sets do not overlap at all. `HeaderedControl.Header`/`Footer` are in
`XamlTypeInfo` and have no attribute; `EmojiGroup`, `RecentEmoji`, `KeyedList`, `KeyedGroup` appear
in `XamlTypeInfo` **zero** times and exist only through the attribute. So the rule for auditing is:

> A binding is safe if its source is a WinRT type, **or** a managed type reached from markup
> (compiler-generated accessor), **or** a managed type carrying the attribute *with that property
> named*. It is at risk only in the last category, and only for properties the attribute missed.

The second clause is what makes the markup surface mostly self-solving and the code-behind surface
dangerous: a binding built in C# is never "reached from markup", so mechanism 1 can never apply to
it. Every code-behind binding against a managed source therefore needs the attribute, with no
fallback — which is exactly how Finding 4 got through.

## Classification of the 108 in markup

**Safe — source is a WinRT type (74 occurrences).** `RelativeSource=TemplatedParent` inside a
`ControlTemplate` whose `TargetType` is a framework control, plus the `TemplateSettings` and
`Source={ThemeResource …}` forms:

`Common/CommonStyles.xaml` 449/470/485/570/1271/2695/2706/2830/2835/2878 ·
`Controls/Chats/ChatSearchBar.xaml` 81/101/120/251 (`TargetType="TextBox"`) ·
`Controls/Messages/Content/PollOptionContent.xaml` 41 ·
`Themes/CheckBox_themeresources.xaml` 584/585/1183/1184 ·
`Themes/Generic.xaml` 266/287/302/418/8244/8823 ·
`Themes/MenuFlyout_themeresources.xaml` 232/408/584/937/1128/1292/1953/1988 ·
`Themes/RadioButton_themeresources.xaml` 123/186/268 ·
`Themes/ScrollBar_themeresources.xaml` 698/699/726/727/1366/1367/1461/1462 ·
`Themes/TeachingTip_themeresources.xaml` 169/177/519 ·
`Themes/TextBox_themeresources.xaml` 170/191/206/309 ·
`Views/Gifts/Popups/GiftCraftChoosePopup.xaml` 66/247 ·
`Views/Host/RootPage.xaml` 154 ·
`Views/Popups/CreatePollPopup.xaml` 580/581 ·
`Views/Settings/SettingsStorageOptimizationPage.xaml` 81/238/243/277/282/580/581 ·
`Views/Settings/SettingsStoragePage.xaml` 88/245/250/284/289/589/590 ·
`Views/Stars/Popups/ResoldGiftsPopup.xaml` 66/247

**Safe — pathless `{Binding}` (3).** No member is resolved; the source object is handed to the
converter or stringified through `IStringable`, which every CCW carries.
`Controls/ProfileHeader.xaml:558`, `Themes/BadgeButton_themeresources.xaml:118` and `:264`.

**Safe — covered by `XamlTypeInfo` (4).** `{Binding Header|Footer, RelativeSource=TemplatedParent}`
on `controls:HeaderedControl`, whose `Header`/`Footer` are `DependencyProperty`s on a managed
`ItemsControl` subclass. Verified: `get_125_HeaderedControl_Header` and `get_126_…_Footer` exist.
`Common/CommonStyles.xaml:345`/`:364`, `Themes/Generic.xaml:3730`/`:3751`.

**Safe — covered by `[GeneratedBindableCustomProperty]` (14).** All grouped-list headers:

| site | path | source type | property present |
|---|---|---|---|
| `Views/Folders/Popups/AddFolderPopup.xaml` 52/53/54/59 | `Key`, `Key.Footer`, `Key.Title` | `KeyedList<KeyedGroup, Chat>` | `Key` ✓, then `KeyedGroup.Title`/`Footer` ✓ |
| `Views/Settings/SettingsSessionsPage.xaml` 47/48/53 | `Key.Footer`, `Key.Title` | `KeyedList<KeyedGroup, Session>` | ✓ |
| `Views/Popups/ContactsPopup.xaml` 57/84/88 | `Key`, `Content.Count`, `Content.Key` | `KeyedList<string, object>` via `ListViewHeaderItem.Content` | `Key` ✓, `Count` ✓ |
| `Views/Supergroups/Popups/SupergroupChooseMemberPopup.xaml` 75/103/107 | same | same | ✓ |

**Dead (4).** Commented out; worth deleting while passing.
`Controls/Messages/Content/LocationContent.xaml:55`/`:81` and
`Controls/Messages/Content/LiveLocationContent.xaml:55`/`:81`.

**At risk (3)** and **resolved safe on inspection (7)** — in the findings below. That accounts for
all 108.

## Bindings built in code-behind

A `Binding` constructed in C# is the same reflective mechanism with none of the markup context, and
it is strictly worse for auditing: the XAML compiler never sees it, so it contributes nothing to
`XamlTypeInfo`, and the source is whatever the expression evaluates to at runtime. Four sites, and
they are the whole of this surface — three `SetBinding` calls and one reflective path, with no
`BindingOperations`, no `XamlReader.Load`, and no `DisplayMemberPath`/`SelectedValuePath` assigned
from code anywhere in the app.

| site | source | path | verdict |
|---|---|---|---|
| `Common/PageBlockRenderer.cs:1472` | `flip`, a `new FlipView()` | `SelectedIndex` | safe — WinRT DP on a WinRT type |
| `Controls/RecentUserHeads.cs:269-282` | `this`, a `RecentUserHeads : Control` | `BorderBrush` | safe — inherited WinRT DP on a managed source; see Finding 3 |
| `ViewModels/StickersViewModel.cs:34` | `MvxObservableCollection<StickerSetViewModel>` | `ItemsPath = "Stickers"` | safe — `Stickers` is the one property that type does register, and the instantiation is in `CsWinRT.cs` |
| `Common/TLNavigationService.cs:173` | `page.DataContext` | `Title` | **broken** — see Finding 4 |

The `StickersViewModel` row is worth a second look because it is the near miss: the same
`StickerSetViewModel` that Finding 1 is about, reached through the same reflective `ItemsPath`, but
asking for the one property that *is* registered. Its view, `StickersPopup.xaml:76`, uses
`x:DataType="viewModels:StickerSetViewModel"` with `{x:Bind Title}` — compiled, so the header text
is fine there. The emoji drawer is the only place that asks for `Title` reflectively.

---

## Finding 1 — `StickerSetViewModel.Title` and `.IsInstalled` are not exposed. Confirmed broken.

`Controls/Drawers/EmojiDrawer.xaml`, the `GroupStyle.HeaderTemplate`:

```xml
<Grid Visibility="{Binding Title, Converter={StaticResource NullToVisibilityConverter}}">   <!-- :150 -->
    <TextBlock Text="{Binding Title}" />                                                     <!-- :156 -->
    <controls:SettingsButton Content="{CustomResource Add}"
        Visibility="{Binding IsInstalled, Mode=OneWay, Converter={StaticResource BooleanNegationConverter}}" />  <!-- :161 -->
```

The chain, each link checked:

- `EmojiCollection.Source` is `EmojiDrawerViewModel.Items`, an `MvxObservableCollection<object>`
  (`EmojiDrawerViewModel.cs:158`).
- That list is heterogeneous: `RecentEmoji` and `EmojiGroup` (`:276`, `:277`), **and
  `StickerSetViewModel`** — `stickers.AddRange(sets)` at `:363`, where `sets` is a
  `List<StickerSetViewModel>` built from `GetInstalledSets()` at `:291`.
- `StickerSetViewModel.Title` (`StickerDrawerViewModel.cs:588`) and `.IsInstalled` (`:584`) both
  exist as ordinary CLR properties.
- `StickerSetViewModel` contributes **no** member accessors to `XamlTypeInfo.g.cs` — it appears
  there only as a type, for `x:DataType`.
- Its generated `GetProperty(string)` in `WinRTCustomBindableProperties.g.cs:207-231` is
  `if (name == "Stickers") { … } return default;` — nothing else.

So on the custom-emoji sets, all three bindings fail. `Visibility="{Binding Title, …}"` at :150
takes the whole header `Grid` with it, so those group headers vanish rather than merely losing
their text. `EmojiGroup` and `RecentEmoji` both expose `Title` **and** `IsInstalled` — the
asymmetry is the proof this is an oversight, not a design.

The attribute is `[GeneratedBindableCustomProperty(new[] { "Stickers" },
new[] { typeof(MvxObservableCollection<StickerViewModel>) })]` at `StickerDrawerViewModel.cs:449`.
The narrow property list is deliberate and right for the *sticker* drawer, whose header uses
`{x:Bind Title}` with `x:DataType="viewModels:StickerSetViewModel"` (`StickerDrawer.xaml:63-67`) —
compiled, free, needs nothing. What was missed is that the *emoji* drawer feeds the same type
through a classic `{Binding}` template.

The second argument is separately inert: that overload takes indexer *parameter* types, and
`StickerSetViewModel` has no indexer, so the generated `GetProperty(Type)` is a bare
`return default;`. Harmless, but it reads as if it does something.

**Fix:** add `"Title"` and `"IsInstalled"` to the name list, and drop the second argument or
replace it with the empty array.

## Finding 2 — `DisplayMemberPath` is a third reflective path, and one instance is unregistered.

`DisplayMemberPath` resolves a property by name at runtime exactly as `{Binding}` does, and no rule
in the analyzer covers it. Two sites:

- **`Views/RevenuePage.xaml:56`** — `DisplayMemberPath="Text"` on a `controls:TopNavView`
  (`TopNavView : ListViewEx`) whose `ItemsSource` is `ObservableCollection<RevenueTabItem>`.
  `RevenueTabItem` (`ViewModels/RevenueViewModel.cs:23`) declares `public string Text { get; }` at
  `:31`, carries **no** `[GeneratedBindableCustomProperty]`, and contributes no `XamlTypeInfo`
  members. The tab strip on the revenue page will render blank labels.
- `Views/Settings/SettingsDataAndStoragePage.xaml:109` — commented out.

The other reflective form, `ItemsPath`, appears three times — `EmojiDrawer.xaml:17`,
`StickerDrawer.xaml:17`, and `StickersViewModel.cs:34` where the `CollectionViewSource` is built in
code. All three ask for `Stickers`, and all three group types expose it. That is the crash the port
already fixed once, and the reason `StickerSetViewModel` has an attribute at all.

### Every by-name surface, swept

`DisplayMemberPath` is not special. The dividing line is whether the XAML compiler can infer the
type whose member is being named:

- **It can**, wherever the type is written down — `Style`/`ControlTemplate` `TargetType` for
  `<Setter Property="…">`, the templated parent for `{TemplateBinding …}`, `x:DataType` for
  `x:Bind`. It emits an accessor into `XamlTypeInfo.g.cs` and nothing more is needed. Confirmed
  both ways in the generated table: `PrefixTextBox2.Prefix`/`PrefixForeground`/`Suffix` are there,
  from `{TemplateBinding}`; `RecentUserHeads.ItemOverlap`/`ItemSize`/`Items` are there, from
  `Setter`s. That is why ~12,500 `{TemplateBinding}` and every `Setter` in the app are free.
- **It cannot**, wherever the name is a bare string applied to *items* of an unknown type. That is
  the whole at-risk set, and it is short:

| surface | sites | verdict |
|---|---|---|
| `{Binding}` in markup | 108 | audited above |
| `Binding` built in C# | 3 | audited above |
| `ItemsControl.DisplayMemberPath` | 1, commented out (`SettingsDataAndStoragePage.xaml:109`) | the live one is fixed |
| `CollectionViewSource.ItemsPath` | 3 | covered — all name `Stickers` |
| `Selector.SelectedValuePath` | 0 | unused, markup and code |
| `AutoSuggestBox.TextMemberPath` | 0 | unused |
| `x:Uid` (MRT sets properties by name) | 0 | unused; the app uses `{CustomResource}` |

Checked and cleared, having looked like candidates:

- **`Storyboard.TargetProperty`** — 1831 uses but only **31 distinct values**, and every one names a
  framework property, either qualified (`(UIElement.RenderTransform).(ScaleTransform.ScaleX)`) or
  bare on a framework target (`Angle` on a `RotateTransform`, `Glyph` on a `FontIcon`). No app type
  is animated by name. Re-check this if an animation is ever pointed at a custom control's own
  property.
- **`{CustomResource}`** — thousands of uses, and `XamlResourceLoader.GetResource` forwards to
  `LocaleService.GetString(key)`, a dictionary and a TDLib call. A resource key, not a member.
- **`Frame.GetNavigationState`/`SetNavigationState`** (`FrameFacade.cs:200`,
  `NavigationService.cs:169`) — resolves *page types* by name rather than members, through the same
  `XamlTypeInfo` name table, and `FrameFacade` already wraps it in a try/catch with a
  GoBack/GoForward fallback and a comment saying it only works for serializable types. Adjacent, not
  a binding, and already defensive.
- **Plain reflection** — zero `Type.GetType`, `Activator.CreateInstance`, `GetProperty`,
  `GetProperties`, `GetMethod`, `ICustomPropertyProvider` or `XamlBindingHelper` anywhere in app
  code, which matches the reflection audit in `net10-port-todo.md`.

## Finding 3 — an inherited WinRT DP on a managed control. Resolved: safe, 8 occurrences.

All three sites bind a property the managed control **inherits from `Control`**, so the member is
declared on a WinRT type but the source instance is managed:

- `Themes/Generic.xaml:393`, `:396`, `:407` — `{Binding Padding, RelativeSource=TemplatedParent}`
  inside `<ControlTemplate TargetType="local:PrefixTextBox2">` (opened at `:144`).
- `Views/MainPage.xaml:454`, `:455`, `:470`, `:471` — `{Binding …, Path=FontSize}` and
  `Path=FontFamily` inside `<ControlTemplate TargetType="controls:NavigationButton">` (`:308`).
- `Controls/RecentUserHeads.cs:269-282` — built in code: `Source = this`,
  `Path = new PropertyPath(nameof(BorderBrush))`, applied to a child `Border`.

None of `PrefixTextBox2.Padding`, `NavigationButton.FontSize`/`FontFamily` or
`RecentUserHeads.BorderBrush` is in `XamlTypeInfo`'s member table, and none of the types carries the
attribute — the compiler emits an accessor only for members *declared* on the app type.
`RecentUserHeads` makes that concrete: it has accessors for its own `ItemOverlap`, `ItemSize` and
`Items`, and none for the inherited `BorderBrush`. Whether the binding engine walks the
`XamlUserType.BaseType` chain up to `Windows.UI.Xaml.Controls.Control` and resolves from system
metadata is the open question, and it is not answerable from generated code.

The generated provider answers it. Two facts, both read out of `XamlTypeInfo.g.cs`:

- `XamlUserType.GetMember(name)` looks the name up in that type's own `_memberNames` and returns
  **null** if it is absent. It does not walk `BaseType`. Confirmed against the three types:
  `PrefixTextBox2` registers only `Prefix`, `PrefixForeground`, `Suffix`, `SuffixForeground`;
  `RecentUserHeads` only `ItemSize`, `ItemOverlap`, `Items`; `NavigationButton` registers no members
  at all.
- Each of their `BaseType`s is a **`XamlSystemBaseType`** — `TextBox`, `Control`,
  `Primitives.ToggleButton` — and every member of that class, `GetMember` included, is
  `throw new NotImplementedException()`. It carries a name and an underlying `Type` and nothing else.

So the framework cannot be calling `GetMember` on a system base type; it recognises one and resolves
from its own native metadata. That is stock generated code, identical in every UWP app, and the
alternative reading would mean any custom control binding an inherited property crashes everywhere.
Reading `Padding` off a `PrefixTextBox2` also never crosses a CCW: the control *is* a `Control`, with
a native peer, so the DP read goes straight to it.

These eight are therefore safe and were left alone. Static reasoning, not a run — but the conclusion
does not depend on this app.

## Finding 4 — the tab header binds `InstantViewModel.Title`, unregistered. Confirmed broken.

`Common/TLNavigationService.cs:173`, in `NavigateToInstant`:

```csharp
tabViewItem.SetBinding(TabViewItem.HeaderProperty, new Binding
{
    Path = new PropertyPath("Title"),
    Source = page.DataContext
});
```

- `page` is the `InstantPage` just navigated to, so `page.DataContext` is an `InstantViewModel`
  (`Views/InstantPage.xaml.cs:47`).
- `InstantViewModel` (`ViewModels/InstantViewModel.cs:22`) is `partial` and declares
  `public string Title` at `:83`.
- It appears in `XamlTypeInfo.g.cs` as a type — `InstantPage` uses `x:Bind` — but with **no** member
  accessor: there is no `case "…InstantViewModel.Title"`. And it carries no attribute; the five
  bindable types are `KeyedList`, `EmojiGroup`, `RecentEmoji`, `StickerSetViewModel`, `KeyedGroup`.

So the binding resolves nothing and the tab loses its title. This is not a corner: every Instant
View article goes through `NavigateToInstant`, which is called from `Common/MessageHelper.cs:497`
and `:579` and `Controls/Cells/SharedLinkCell.xaml.cs:284`, and `NavigateToTab` either adds a tab to
the existing Instant View window or opens one. The `Header = "Test"` set a few lines earlier is a
placeholder the binding is meant to replace.

This is the site that most justifies auditing code-behind separately. It is the only binding in the
app whose source is a **page view model**, and it is invisible to every static signal used above:
no markup, no `x:DataType`, no attribute, and the property name is a string literal three files away
from the type it resolves against.

## Corroboration for a known open item

Two of the pages audited here also assign an unregistered collection instantiation, which is the
"83 app collections still unregistered" item in `net10-port-todo.md` rather than a new finding:

- `AddFolderPopup.xaml:17` — `Source="{x:Bind ViewModel.Items}"` where `Items` is
  `MvxObservableCollection<KeyedList<KeyedGroup, Chat>>`.
- `SettingsSessionsPage.xaml:14` — the same shape with `KeyedList<KeyedGroup, Session>`.
- `RevenuePage.xaml:54` — `ObservableCollection<RevenueTabItem>` into `ItemsSource`.

`CollectionViewSource.Source` and `ItemsControl.ItemsSource` are both typed `object`, so `TG1001`
should name all three; none was in `CsWinRT.cs`. That points at the analyzer needing a re-run rather
than at a gap in it — which the todo already says to do, and these three make a good check that the
re-run is honest. The `RevenuePage` one is now added by hand, for the reason in the list below; the
two grouped ones are left for the re-run.

Convenient consequence: opening either page exercises the collection registration *and* the
`{Binding}` paths at once, so one test covers both items.

## What to do

- [x] **Finding 1 — the emoji drawer header is now `x:Bind`.** The three group types gained a
      shared `IDrawerGroup { Title, IsInstalled }` (`EmojiDrawerViewModel.cs`), and the header
      template took `x:DataType="viewModels:IDrawerGroup"`. Registering `Title` and `IsInstalled`
      on `StickerSetViewModel` would have worked too, but it would have left a third reflective
      surface with nothing to check it; the interface makes the header compile-checked and matches
      what the sticker drawer already does. `StickerSetViewModel` keeps `["Stickers"]` — that one
      is still resolved by name, by `ItemsPath` — and its inert second argument is now `Type[] { }`.
- [x] **Finding 2 — `DisplayMemberPath` replaced by an `ItemTemplate`** on `RevenuePage.xaml`,
      `x:DataType="viewModels:RevenueTabItem"` with `{x:Bind Text}`. That is what
      `DisplayMemberPath` built anyway, so the rendering is unchanged and `RevenueTabItem` needs no
      attribute.
- [x] **Finding 4 — the tab header binding stays, and `InstantViewModel` is registered for
      `Title` alone.** The title arrives asynchronously, so an assignment would not do, and a
      `PropertyChanged` subscription from inside a local function cannot be unsubscribed without a
      lambda. The binding is now typed against `InstantPage.ViewModel` and uses `nameof` instead of
      a string literal. The `Header = "Test"` placeholder went with it.
- [x] Deleted the four commented-out bindings in `LocationContent.xaml` and
      `LiveLocationContent.xaml`.
- [x] **Finding 3 answered from `XamlTypeInfo.g.cs` instead of by running** — safe, and all eight
      occurrences left as they are. See the finding for the two facts that settle it.
- [x] `ObservableCollection<Telegram.ViewModels.RevenueTabItem>` added to `CsWinRT.cs`. This is the
      one hand-added entry in that block and it is deliberate: without it `RevenuePage` cannot set
      `ItemsSource` at all, so the `ItemTemplate` fix above could not be seen. It is also the one
      case with no ambiguity — the analyzer's own remarks say CsWinRT generates vtables for
      "non-generic types declared in this assembly, and for nothing else", `ItemsSource` is typed
      `object`, and `RevenueTabItem` itself is non-generic and local so it needs nothing.
- [ ] Regenerate the block — but **not from `develop` as it stands**. A full
      `Build.Modern.ps1` on 2026-08-17 ran the analyzer over the whole app and reported **zero**
      `TG1001`/`TG1002`/`TG1003`, which is not clearance: `develop` was reset to `2c74f6266` and
      lost `4cbb362fc`, the commit that taught `IsBoundary` to follow x:Bind into
      `XamlBindingSetters`. Without it nothing assigned by `{x:Bind}` is visible to the rule, and
      that is the shape of all three collections here. The commit survives on branch
      `tdlib-list-t`. Restore it before trusting a re-run; the two grouped collections were left
      un-added because whether the inner `KeyedList<,>` needs its own entry is the analyzer's
      question, not a reader's.

### A trap this turned up: `{Binding}` → `{x:Bind}` is not a mechanical swap

`x:Bind` **casts the converter's return value to the target type**; classic `{Binding}` puts it
through `SetValue`, which coerces. So a converter that returns the wrong type works under one and
throws `InvalidCastException` under the other.

`BooleanNegationConverter.Convert` returns a **`bool`**, and the emoji drawer's header was assigning
it to `Visibility`. Swapping that one attribute to `x:Bind` verbatim would have traded a silently
blank header for a hard failure. It now uses `BooleanToVisibilityConverter` with
`ConverterParameter=invert`, which returns `Visibility` — the same binding
`StickersPopup.xaml:91` already uses on the same property of the same type.

`NullToVisibilityConverter` returns `Visibility` on every path, so `Title` needed no such change.
Check the converter's return type on every future swap; there are only twelve converters in
`Converters\`, and `BooleanNegationConverter` is the one that bites.
- [ ] Consider teaching the analyzer the by-name surfaces — `DisplayMemberPath`,
      `SelectedValuePath`, `TextMemberPath`, `ItemsPath` — and the code-behind `SetBinding` shape.
      Unlike `{Binding}` these carry a string literal and a resolvable source, so a rule is feasible
      where one for `{Binding}` is not. Four live sites today across the lot, but two of the three
      defects in this audit were that shape, and the sweep above is the list a rule would encode.

## What can and cannot be automated

A rule for classic `{Binding}` would need the `DataContext` type at each markup site, which is
precisely what classic binding does not declare — that is the whole difference from `x:Bind`. The
tractable half is what this audit did by hand: the source type is knowable wherever the binding sits
in a `ControlTemplate` (from `TargetType`) or a `GroupStyle.HeaderTemplate` (from the
`CollectionViewSource` feeding it). Twenty-six files is small enough to re-audit; the thing to watch
is a *new* `{Binding}` against a runtime-only type, and the cheapest guard against that is to keep
preferring `x:Bind` with an explicit `x:DataType`, as 2038 of the app's 2149 bindings already do.

The code-behind sites are the opposite case and the more dangerous one, even at three occurrences.
There the analyzer *could* help: `SetBinding(dp, new Binding { Path = new PropertyPath("X"), Source = expr })`
is ordinary C# with a literal path and a typed source expression, which is exactly what a Roslyn
rule reads. `Source = page.DataContext` types as `object` and would have to warn rather than
resolve — but "a Binding whose Source is not provably a WinRT type" is a fair thing to warn on when
there are three of them in the whole app. Worth folding into `WinRTExposedTypeAnalyzer` alongside
the `DisplayMemberPath` idea above; between them they would cover every reflective path in the app
except classic `{Binding}` itself.
