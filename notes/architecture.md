# Codebase map

Where things live, what owns what, and the traps in each subsystem. This is an index, not
documentation: enough to know which files to open and what to be careful of, no more.

**How to use it.** Read the section for the area you are about to touch before exploring it.
Every section names the types worth knowing, how the rest of the app reaches them, and the
constraints that a grep will not show you.

**How to trust it.** It is hand-verified, not generated, so it can be wrong. Each section
carries the commit it was last checked against:

    <!-- map: verified=<sha> paths=<comma-separated prefixes> -->

A `SessionStart` hook (`.claude/hooks/map-staleness.ps1`) compares those paths against HEAD
and names the sections whose files have moved since. A stale section is a lead, not a fact.

**How to keep it.** When a change alters what a section describes, update that section and
its `verified=` SHA in the same commit as the change — never as a separate "update the map"
commit. Refresh is per-section, so a section nobody touched costs nothing.

For deep dives — reviews, migrations, open todo lists — see the rest of `notes/`, indexed at
the end of this file.

---

# Startup and shell

## App startup — Telegram/Program.cs, Telegram/App.xaml.cs, Telegram/Host/, Telegram/Constants.cs (14 files)
<!-- map: verified=95560d9f7 paths=Telegram/Program.cs,Telegram/App.xaml.cs,Telegram/App.xaml,Telegram/Host,Telegram/Constants.cs -->
Two entry points into the same `App`. `Program.cs` (guarded by `DISABLE_XAML_GENERATED_MAIN`)
is the UWP-packaged one: initializes `AnimationEffects`, then `Application.Start`.
`Telegram/Host/Program.cs` is the Win32/unpackaged one: fixes the working directory, sets DPI
awareness, creates a `DispatcherQueue` and installs a `DispatcherQueueSynchronizationContext`,
constructs `App`, initializes `WindowsXamlManager`, starts from `AppInstance.GetActivatedEventArgs()`,
then runs its own Win32 message loop over `IslandWindow.All`.
**Key types:** `App` (Telegram/App.xaml.cs) — activation, theming, and the `ViewModelForPage`
resolver switch; `Program` (Telegram/Host/Program.cs) — Win32 island host and message loop;
`IslandWindow` (Telegram/Host/IslandWindow.cs) — one HWND plus a `DesktopWindowXamlSource`;
`Constants` (Telegram/Constants.cs) — api id/hash, build flags, animation durations, media filters.
**Entry points:** the OS launches the packaged UWP entry or, unpackaged, `Telegram.Host.Program.Main`;
both construct `App` and go through the `BootStrapper`/`WindowContext` machinery. `App.OnStart`
branches on `StartKind` and on `args is ShareTargetActivatedEventArgs` to build a `RootWindow`
or a `ShareWindow`; `OnBackgroundActivated` handles app-service connections and toast actions.
**Traps:** the Win32 host calls `TerminateProcess` instead of returning — normal shutdown
ordering crashes XAML. The `DispatcherQueueSynchronizationContext` must be installed before any
TDLib await, or continuations resume on a TDLib thread. Hosted views (the share target) get no
synchronization context from the generated Main; see `notes/` on that.

---

# The spine: TDLib, services, collections

## TDLib interop — Telegram/Td/ (6 files outside Api/), Telegram.Generators/ (8 files)
<!-- map: verified=95560d9f7 paths=Telegram/Td/Client.cs,Telegram/Td/ClientJson.cs,Telegram/Td/PtrClientJson.cs,Telegram/Td/TdJsonReader.cs,Telegram.Generators -->
The managed binding to `tdjson.dll`, and the source generator that turns `td_api.tl` into the
~3000-class `Telegram.Td.Api` surface plus its JSON parsers. The generated code is not checked
in — it lands under `obj/<config>/<tfm>/generated/`.
**Key types:** `Client` (Telegram/Td/Client.cs) — the P/Invoke surface (`td_send`/`td_receive`/
`td_execute`/`td_create_client_id`) via `[LibraryImport]` and function pointers, not delegate
marshalling; `ClientResultHandler` (same file) — the callback interface `ClientService` implements;
`TdJsonReader` (Telegram/Td/TdJsonReader.cs) — pointer-based reader over TDLib's receive buffer;
`SchemaGenerator` (Telegram.Generators/SchemaGenerator.cs) — incremental generator over
`Libraries/tdjson/td_api.tl`; `ApiEmitter` (Telegram.Generators/Emit/ApiEmitter.cs); `TlSchema`/`Naming`
(Telegram.Generators/Schema/) — schema model and C# naming rules.
**Entry points:** driven by the `<AdditionalFiles Include="…td_api.tl">` item and the `<TdParsers>`
MSBuild property. App code just references `Telegram.Td.Api.*`.
**Traps:** only one parser variant builds into the app at a time — two parsers for one schema is
~44k lines of .NET Native compile time for a set nothing calls. `Telegram.Benchmarks` is the only
project that builds `Both`. `SchemaGenerator` emits `TDAPI003` when a vector instantiation is
missing from `CsWinRT.Vectors.cs`.

## Vector<T> — Telegram/Td/Vector.cs
<!-- map: verified=95560d9f7 paths=Telegram/Td/Vector.cs -->
Replaces `IList<T>`/`List<T>` on every TDLib vector field. Declared in the root `Telegram`
namespace, not `Telegram.Td`, so it wins name resolution against `System.Numerics.Vector<T>`
without a `using`.
**Key types:** `Vector<T>` (Telegram/Td/Vector.cs) — read-only `IReadOnlyList<T>`, not sealed, with
a shared `Vector<T>.Empty` per element type; `MutableVector<T>` — the only writable subclass, used
when building a request argument.
**Traps:** the parser materializes tens of thousands of vectors a minute and most are empty, so it
hands out a shared empty instance — which is only safe if the compiler, not convention, bars
mutation through the base type. Hence no mutating members on `Vector<T>` at all; `IList<T>` support
lives on `MutableVector<T>`.

## WinRT AOT exposure manifest — Telegram/CsWinRT.cs, CsWinRT.Vectors.cs, Telegram.Generators.WinRT/ (4 files)
<!-- map: verified=95560d9f7 paths=Telegram/CsWinRT.cs,Telegram/CsWinRT.Vectors.cs,Telegram.Generators.WinRT -->
A manifest of every constructed generic and array type that crosses the WinRT ABI (XAML bindings,
`ItemsSource`, boxed `object`). AOT only generates CCW vtables for types the compiler sees converted
in source, so anything boxed dynamically must be declared here.
**Key types:** `CsWinRT.cs` — the `[assembly: GeneratedWinRTExposedExternalType(typeof(…))]` list,
plus the global aliases that resolve TDLib-vs-WinRT name collisions (`CompositionTarget`, `Object`,
`User`, `TimeZone`); `WinRTExposedTypeAnalyzer` (Telegram.Generators.WinRT/) — analyzer (TG1001/TG1002)
flagging a managed collection boxed into WinRT with no matching entry; `DynamicCastRootGenerator`
(same project) — emits `[DynamicDependency]` roots so trimming cannot remove metadata a cast needs;
`WinRTProjection` — shared projection helpers.
**Traps:** a missing entry is silent at compile time and fails at runtime as `E_INVALIDARG` out of
`set_ItemsSource` — which, on a DispatcherQueue callback, fail-fasts the process rather than throwing.
Regenerate the list from the analyzer output; do not hand-sweep it. `DynamicCastRootGenerator` emits
nothing under .NET Native, where nothing is trimmed.

## Services (core) — Telegram/Services/ (45 files at root)
<!-- map: verified=01cb6feb8 paths=Telegram/Services/ClientService.cs,Telegram/Services/EventAggregator.cs,Telegram/Services/OptionsService.cs,Telegram/Services/DownloadsService.cs,Telegram/Services/NotificationsService.cs,Telegram/Services/PlaybackService.cs -->
The account-scoped service layer. Each session owns one `ClientService` (TDLib client plus
in-memory cache) and companion services for downloads, contacts, locale, notifications, options,
playback, proxy, generation, forums and saved messages. `ClientService` is the largest type in the
app, split into partials by feature area.
**Key types:** `ClientService` (Telegram/Services/ClientService.cs) — client, cache, dispatch;
`ClientService.ChatList.cs` — chat-list ordering and positions per `ChatList`; `ClientService.Files.cs`
— download bookkeeping and on-disk reconciliation; `ClientService.ForumTopics.cs`/`.SavedMessages.cs`/
`.StoryList.cs`/`.FeedbackChatTopics.cs` — per-feature caches; `OptionsService` — TDLib global options;
`EventAggregator` (Telegram/Services/EventAggregator.cs) — the pub/sub bus.
**Entry points:** view models and pages get `IClientService`/`ISession` by DI (Session.Registrations.cs);
`ClientService.Send`/`SendAsync` is the only path to TDLib; `EventAggregator` is how anything subscribes
to updates.
**Traps:** `OnResult` runs on the dedicated static `_runThread` ("TdReceive"), started once in the static
constructor and shared by every client and session — update handlers touching UI objects must marshal
explicitly. `EventAggregator.Publish` invokes subscribers synchronously on the TDLib thread. File
updates do not go through it at all — `ClientService.UpdateFile` hands them to `UpdateManager`, which
is their own bus (see *Common helpers*).

## Settings, theme and updates — Telegram/Services/{AppSettings.cs,SettingsService.cs,Settings/,Theme/,Updates/} (26 files)
<!-- map: verified=95560d9f7 paths=Telegram/Services/AppSettings.cs,Telegram/Services/SettingsService.cs,Telegram/Services/SettingsLegacyService.cs,Telegram/Services/SettingsSearchService.cs,Telegram/Services/Settings,Telegram/Services/Theme,Telegram/Services/ThemeService.cs,Telegram/Services/Updates,Telegram/Services/CloudUpdateService.cs -->
A two-tier split: a static, process-wide `AppSettings`, and a per-account `SettingsService` over an
`ISettingsStore` seam. Plus theme resolution and the sideload updater.
**Key types:** `AppSettings` (Telegram/Services/AppSettings.cs) — static global config, no DI;
`SettingsService`/`ISettingsService` (Telegram/Services/SettingsService.cs) — per-session settings, keyed
by numbered container; `ISettingsStore`/`ApplicationDataSettingsStore` (Telegram/Services/Settings/SettingsStore.cs)
— the storage abstraction; the typed groups (`AutoDownloadSettings`, `NotificationsSettings`, …) in
Telegram/Services/Settings/; `ThemeService`/`ThemeLookup`/`ThemeAccentInfo`; `CloudUpdateService`.
**Traps:** containers are keyed by `$"{session}"` and must exist on disk before `ClientService` is
constructed — `LifetimeService` discovers sessions from those folders before any `SettingsService`
exists, and `ClientService` reads `UseTestDC` while being constructed. `ISettingsStore` is the single
seam for swapping backends. `settings.dat` is locked while the package is registered.

## Session and account multiplexing — Telegram/Services/LifetimeService.cs, Session.cs, Session.Registrations.cs
<!-- map: verified=95560d9f7 paths=Telegram/Services/LifetimeService.cs,Telegram/Services/Session.cs,Telegram/Services/Session.Registrations.cs -->
One `ISession` per TDLib instance, each with its own `ClientService`, `ISettingsService` and
`IEventAggregator`, in one registry.
**Key types:** `ILifetimeService`/`LifetimeService` — owns `ReaderWriterDictionary<int, ISession>`,
discovers sessions from on-disk settings containers, exposes `ActiveItem`/`Items`/`Count`;
`ISession`/`SessionImpl` (Telegram/Services/Session.cs) — the per-account facade and `Resolve<T>`;
`Session.Registrations.cs` — per-session DI wiring.
**Entry points:** windows and pages resolve the account through `NavigationService.Session`; switching
accounts sets `LifetimeService.ActiveItem`, flipping `IsActive`/`Options.Online` on both sessions.
**Traps:** `AccountsSelectorOrder`/`ActiveSession` live in the static `AppSettings`, not per-session
state. `SessionImpl.Handle(UpdateAuthorizationState)` drives destruction, and proactively sends
`Destroy` for a background account that reaches `AuthorizationStateWaitPhoneNumber` while more than
one session exists.

## Collections — Telegram/Collections/ (36 files)
<!-- map: verified=95560d9f7 paths=Telegram/Collections -->
The collection toolkit behind every list: update-driven diffing for the chat list, search and settings
lists, and cursor-based incremental loading for the message list and other paged sources.
**Key types:** `DiffObservableCollection<T>` (Telegram/Collections/DiffObservableCollection.cs) —
diff/patch collection with move detection over `RangeObservableCollection<T>`; `IDiffHandler<T>`/
`DiffCalculator`/`DiffUtil` (Telegram/Collections/Diff/) — the algorithm and per-type comparison
contract; `ChatDiffHandler`, `MessageDiffHandler`, `SearchResultDiffHandler` (Telegram/Collections/Handlers/);
`IncrementalCollection<T>`/`IncrementalCollectionView<…>` — `ISupportIncrementalLoading` paging;
the `Search*Collection` family; `KeyedList<TKey,T>`; `GroupCallParticipantsCollection`.
**Entry points:** view models construct these directly and bind them straight to `ItemsSource`; TDLib
updates arrive via `ClientService`/`EventAggregator` and are applied through diff calls.
**Traps:** any of these generic instantiations crossing the WinRT ABI needs a matching `CsWinRT.cs`
entry — several closures are listed there precisely because a binding assigns through `ItemsSource`
and the analyzer cannot otherwise see the concrete type. A ListView needs one notification per item;
`Reset` is not a substitute.

---

# UI: pages, view models and navigation

## Page and ViewModel base architecture — Telegram/Navigation/ViewModelBase.cs, MultiViewModelBase.cs, Telegram/Controls/FrameworkElementEx.cs, Telegram/Views/HostedPage.cs
<!-- map: verified=95560d9f7 paths=Telegram/Navigation/ViewModelBase.cs,Telegram/Navigation/MultiViewModelBase.cs,Telegram/Controls/FrameworkElementEx.cs,Telegram/Views/HostedPage.cs -->
The MVVM contract every page follows. `ViewModelBase` carries `IClientService`, `ISettingsService`,
`IEventAggregator` and `INavigationService`. Pages derive from `PageEx` — a `Page` with
`Connected`/`Disconnected` events — or from `HostedPage : PageEx` for pages living inside a tabbed or
master-detail frame, which adds title, header visibility and scroll-position persistence
(`GetPosition()`/`SetPosition()`).
**Key types:** `ViewModelBase` (Telegram/Navigation/ViewModelBase.cs) — base for every page and popup
view model; `MultiViewModelBase` (Telegram/Navigation/MultiViewModelBase.cs) — a parent that fans
lifecycle out to `Children`; `PageEx` and `ContentDialogEx` (Telegram/Controls/FrameworkElementEx.cs:220)
— the loaded-state-tracking Page and ContentDialog bases; `HostedPage` (Telegram/Views/HostedPage.cs);
`ProfileTabsViewModel`/`MediaTabsViewModelBase` (Telegram/ViewModels/Profile/ProfileTabsViewModel.cs) —
the tabbed-profile view model over `MultiViewModelBase`.
**Traps:** `MultiViewModelBase.Children` must be populated before navigation, or `Dispatcher`,
`NavigationService`, `NavigatedToAsync` and `NavigatedFrom` never reach the child tab view models.

## The delegate pattern — Telegram/ViewModels/Delegates/ (25 files)
<!-- map: verified=95560d9f7 paths=Telegram/ViewModels/Delegates,Telegram/ViewModels/MessageDelegate.cs -->
A push-update channel running parallel to data binding. Each `IViewModelDelegate` interface declares
`UpdateXxx(...)` methods the view model calls directly on the live page or control, instead of going
through `INotifyPropertyChanged` — used for high-frequency and cell-targeted updates (message bubbles,
profile header, chat list rows).
**Key types:** `IDelegable<TDelegate>` (Telegram/ViewModels/Delegates/IDelegable.cs) — the `Delegate`
property contract view models implement; `IViewModelDelegate` (same file) — the marker all delegate
contracts derive from; `IProfileDelegate`, `ISettingsDelegate`, `IDialogDelegate`, `IChatDelegate`,
`IUserDelegate`, `ISupergroupDelegate` (Telegram/ViewModels/Delegates/); `MessageDelegate`
(Telegram/ViewModels/MessageDelegate.cs).
**Entry points:** `ISession.Resolve<TViewModel, TDelegate>(TDelegate page)` (Telegram/Services/Session.cs)
sets `viewModel.Delegate = page` at resolve time, the page itself implementing the matching interface;
`HostedPage.AssignDelegate` re-wires it when a hosted page's content or DataContext is swapped.
**Traps:** pages must null out `ViewModel.Delegate` in `OnNavigatedFrom`, or the view model holds a dead
UI reference. `Resolve<T, TDelegate>` constrains `T : IDelegable<TDelegate>`, so the page-to-delegate
mapping is compile-time checked in the `App.xaml.cs` switch.

## ViewModel resolution and DI — Telegram/App.xaml.cs, Telegram/Navigation/BootStrapper.cs, Telegram/Services/Session.Registrations.cs
<!-- map: verified=95560d9f7 paths=Telegram/Navigation/BootStrapper.cs -->
The mapping from page or popup `Type` to concrete view model, and the source-generated container that
constructs it with session-scoped services injected.
**Key types:** `BootStrapper.ViewModelForPage(UIElement, ISession)` (Telegram/Navigation/BootStrapper.cs)
— the virtual hook; `App.ViewModelForPage` (Telegram/App.xaml.cs) — one large switch expression mapping
every page and popup to `session.Resolve<VM>()` or `session.Resolve<VM, TDelegate>(page)`;
`[GenerateResolver(...)]` on `SessionImpl` (Telegram/Services/Session.Registrations.cs) — the declarative
Singletons/Lazy/Instances list a generator turns into `Resolve<T>()`.
**Entry points:** `NavigationService` calls `BootStrapper.Current.ViewModelForPage(page, Session)` on
first navigation to a page and on popup show.
**Traps:** a new view model must be added to the `Instances` list in Session.Registrations.cs, and
usually a case in the App.xaml.cs switch, or it is unresolvable — `Resolve<T>()` returns null silently.
Each `Resolve<T>()` builds a fresh instance (per-navigation lifetime); only `Singletons`/`Globals` are
shared.

## Navigation service — Telegram/Navigation/Services/, Telegram/Common/NavigationService.cs, TLNavigationService.cs
<!-- map: verified=95560d9f7 paths=Telegram/Navigation/Services,Telegram/Common/NavigationService.cs,Telegram/Common/TLNavigationService.cs,Telegram/Common/TLRootNavigationService.cs -->
Wraps the XAML `Frame` with back-stack bookkeeping, view model lifecycle dispatch, popup presentation and
secondary-window opening. `TLNavigationService` is the app's concrete subclass, adding the Telegram-specific
destinations (web apps, instant view, invoices, text editor windows).
**Key types:** `INavigationService`/`NavigationService` (Telegram/Navigation/Services/NavigationService.cs)
— Navigate, GoBack, ShowPopupAsync, OpenAsync, back stack; `INavigable`
(Telegram/Navigation/Services/INavigable.cs) — the `NavigatedToAsync`/`NavigatedFrom`/`NavigatingFrom`
contract `ViewModelBase` implements; `FrameFacade` (…/FrameFacade.cs) — thin `Frame` wrapper firing
Navigating/Navigated; `TLNavigationService` (Telegram/Common/TLNavigationService.cs);
`NavigationStackItem` — a back-stack entry carrying page type, parameter, title and scroll position.
**Entry points:** view models call `NavigationService.Navigate(type, parameter)` or the extensions in
Telegram/Common/NavigationService.cs. `OnNavigated` resolves the view model, sets `NavigationService` and
`Dispatcher` on it, then awaits `NavigatedToAsync(parameter, mode, pageState)`.
**Traps:** `SettingsPasswordPage`/`SettingsPasscodePage` are excluded from the forward stack on back
navigation (`_unallowedTypes`), so a user cannot navigate forward into a password screen.
`ViewModelForPage` only creates a view model when `DataContext` is not already `INavigable`, so
re-entering a cached page reuses the existing instance.

## Popup hosting — Telegram/Controls/ContentPopup.cs, Telegram/Views/Popups/ (166 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/ContentPopup.cs,Telegram/Controls/ContentPopup.Win32.cs,Telegram/Controls/MessagePopup.xaml.cs,Telegram/Controls/ToastPopup.cs,Telegram/Views/Popups -->
`ContentPopup : ContentDialogEx` (Telegram/Controls/ContentPopup.cs:39) is the base for essentially every
popup in the app — the 166 under Views/Popups plus those scattered through Settings, Supergroups and
Stars. It adds view model binding, queued one-at-a-time presentation, and theme and animation plumbing.
**Key types:** `ContentPopup` (Telegram/Controls/ContentPopup.cs) — `ShowQueuedAsync`, `OnNavigatedTo`/
`OnNavigatedFrom`, `IsAnyPopupOpen`; `MessagePopup` (Telegram/Controls/MessagePopup.xaml.cs) — the generic
message box behind `ShowPopupAsync(string message, …)`; `ToastPopup` (Telegram/Controls/ToastPopup.cs) —
the non-modal toast behind `NavigationService.ShowToast`.
**Entry points:** `ViewModelBase.ShowPopupAsync(...)` delegates to `INavigationService.ShowPopupAsync`,
which resolves a view model through the same `ViewModelForPage` switch as pages, sets `DataContext`, calls
`NavigatedToAsync` and `OnNavigatedTo`, then `ShowQueuedAsync(XamlRoot)`.
**Traps:** `ShowQueuedAsync` serializes popups per `XamlRoot` — a second popup awaits the first's
`_closingTask` rather than stacking. `Closed` only tears the view model down (`NavigatedFrom`) when
`IsFinalized` is true, so a popup that is reused or re-shown must manage that flag. Popups need the chat
theme forwarded to them explicitly.

## WindowContext and secondary windows — Telegram/Navigation/WindowContext*.cs, Telegram/Services/ViewService/
<!-- map: verified=95560d9f7 paths=Telegram/Navigation/WindowContext.cs,Telegram/Navigation/WindowContext.Uwp.cs,Telegram/Navigation/WindowContext.Win32.cs,Telegram/Services/ViewService -->
`WindowContext` is the per-window state object — dispatcher, XamlRoot, activation and visibility events —
plus the static registry (`All`, `Active`, `Main`) used to find and switch between open windows.
`IViewService.OpenAsync` is how a new secondary window is created (calls, web apps, text editor, tabbed
browser).
**Key types:** `WindowContext` (Telegram/Navigation/WindowContext.cs) — window and session state, with
`ForEach`/`ForEachAsync`/`ForXamlRoot` lookups; `IViewService`/`ViewService`
(Telegram/Services/ViewService/ViewService.Uwp.cs, ViewService.Win32.cs) — `OpenAsync(ViewServiceOptions)`
creates a `CoreApplication.CreateNewView()` thread and window on UWP, an island on Win32;
`ViewServiceOptions` — size, persisted id, `ViewMode` and the content factory for the new window's root.
**Entry points:** `INavigationService.OpenAsync(...)` forwards to `viewService.OpenAsync`;
`TLNavigationService.NavigateToWebApp`/`NavigateToTextEditor`/`NavigateToInvoice` build the options and
first probe `WindowContext.ForEachAsync` to reuse a matching open window via
`ApplicationViewSwitcher.SwitchAsync`.
**Traps:** on UWP `ApplicationView.GetForCurrentView()` throws for a hosted view, so
`ViewService.OnWindowCreated` branches on `view.IsHosted` before touching `ViewLifetimeControl`. Each
secondary window runs its content-creation callback on its own thread and dispatcher, so cross-window
updates must use `WindowContext.Dispatcher`, not the calling page's. See `window-lifetime-review.md` at
the repo root for the open items here.

---

# UI: the chat list

## Chat and topic list cells — Telegram/Controls/Cells/ (108 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Cells -->
The visual row for every chat-list, topic-list and forum entry: title, preview, ticks, unread badges,
typing indicator, mute icon, avatar and story ring. These are plain `Control`/`Grid` subclasses, not
ListViewItems, reused as `ContentTemplateRoot` inside recycled containers.
**Key types:** `ChatCell` (Telegram/Controls/Cells/ChatCell.xaml.cs) — chat-list row, implements
`IMultipleElement`; `ForumTopicCell` (…/ForumTopicCell.xaml.cs) — implements `IForumTopicDelegate`;
`ChatFolderCell` (…/ChatFolderCell.xaml.cs); `ActiveStoriesCell` (…/ActiveStoriesCell.xaml.cs).
**Entry points:** `ChatListListView.OnContainerContentChanging`/`OnChoosingItemContainer`
(Telegram/Controls/ChatListListView.cs:237,250) casts `args.ItemContainer.ContentTemplateRoot` to
`ChatCell` and calls `UpdateChat`/`UpdateChatList`; `ForumView.OnContainerContentChanging`
(Telegram/Controls/Views/ForumView.xaml.cs:261) does the same for `ForumTopicCell`.
**Traps:** cells are never constructed per item — one instance is mutated through many narrow
`Update*` methods fired from TDLib update handlers, so missing one when adding a field silently
leaves stale UI. `args.InRecycleQueue` must short-circuit before touching content.

## Message list virtualization and scrolling — Telegram/Controls/Chats/ChatHistoryView.cs, ChatHistoryViewItem.cs
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Chats/ChatHistoryView.cs,Telegram/Controls/Chats/ChatHistoryViewItem.cs -->
The bubble list (a `ListViewEx` subclass): bidirectional incremental loading, scroll anchoring around
content changes, multi-select drag gestures, and scroll-to-message with retry and fast paths.
**Key types:** `ChatHistoryView` — the ListView, owns `ScrollingHost`/`ScrollingPropertySet`;
`BidirectionalIncrementalLoader` (same file, ~line 834) — dynamic-threshold prefetch driven by
`ViewModel.HasMoreItemsAtTop/Bottom`; `ChatHistoryViewItem` — `ListViewItem` subclass carrying padding
and automation naming; `ChatListViewAutomationPeer` (same file) — reaches into `ContentTemplateRoot`
for accessible names.
**Traps:** `Disconnect()` may only be called on page unload — setting `ItemsSource = null` there is a
deliberate workaround for the ListView otherwise hoarding hundreds of realized containers (comment at
:122-137). `ScrollIntoViewAsync` calls `panel.UpdateLayout()` right after `ScrollIntoView` to force
synchronous container realization (~line 538); removing it breaks scroll-to-message. `SetScrollingMode`
is a two-phase queue because `ItemsPanelRoot` may not exist yet. The saved-messages tab inverts
`KeepLastItemInView`/`KeepItemsInView` (~line 357) because it scrolls the other way.

## Composer and chat chrome — Telegram/Controls/Chats/ (~35 further files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Chats -->
Everything around the message list but not the list: the input box and its record/send state machine,
in-chat banners (translate, join requests, pinned message, sponsored, connected bot, group call), and
the wallpaper renderer.
**Key types:** `ChatTextBox` (Telegram/Controls/Chats/ChatTextBox.cs) — the rich text composer;
`ChatRecordBar` (…/ChatRecordBar.xaml.cs) — voice/video recording UI with composition-driven waveform
and blob visuals; `ChatActionBarView` (…/ChatActionBarView.xaml.cs); `ChatBackgroundControl`/
`ChatBackgroundPresenter` — the wallpaper compositor.
**Traps:** `ChatRecordBar` holds raw `Visual` handles alongside a `CaptureElement`/`MediaCapture`;
these must be disposed on stop or the mic session leaks.

---

# UI: messages

## MessageBubble pipeline — Telegram/Controls/Messages/ (MessageBubble, MessageBubblePanel, MessageSelector, MessageContentRecyclePool, IContent)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Messages/MessageBubble.xaml.cs,Telegram/Controls/Messages/MessageBubble.xaml,Telegram/Controls/Messages/MessageBubblePanel.cs,Telegram/Controls/Messages/MessageSelector.xaml.cs,Telegram/Controls/Messages/MessageContentRecyclePool.cs,Telegram/Controls/Messages/IContent.cs -->
Renders one regular message: reply and forward headers, text, media content, footer and reactions in a
single custom-panel layout. `MessageSelector` is the ListView item's `ContentTemplateRoot` — selection
chrome and the recycling entry point — hosting either a `MessageBubble` or a `MessageService`.
**Key types:** `MessageBubble` (Telegram/Controls/Messages/MessageBubble.xaml.cs, 4125 lines) —
assembles every sub-part; `MessageBubblePanel` — custom Measure/Arrange placing the text/footer overlap;
`IContent`/`IContentWithFile`/`IContentWithMask`/`IContentWithPlayback` (…/IContent.cs) — the contract
every media content control implements; `MessageContentRecyclePool` — per-kind bounded stack of retired
`IContent` controls; `MessageSelector` — container root, owns selection state.
**Entry points:** `Telegram/Views/ChatView.Bubbles.xaml.cs` `OnContainerContentChanging` sets
`container.ContentTemplateRoot` → `MessageSelector.Content` → `MessageBubble`/`MessageService`;
`UpdateContentRecyclePool` wires one shared pool per list. `MessageBubble.UpdateMessageContentControl`
switches on `message.GeneratedContent ?? message.Content` to build the right `Content/*` control.
**Traps:** `MessageContentRecyclePool.Put` requires the caller to have called `IContent.Recycle()` first
— the pool holds "no message, no subscription" controls. Capacity is fixed at 8 per `Type`, deliberately
bounded because Activator-based construction would break the AOT build. `MessageBubblePanel` assumes a
fixed Children ordering (`[factCheck?] text media [reactions?] footer`); reordering the template breaks
layout silently.

## Message content implementations — Telegram/Controls/Messages/Content/ (52 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Messages/Content -->
One `IContent` control per `MessageContent` td-api case — photo, video, sticker, poll, checklist,
invoice, location, dice, giveaway, webpage/instant view, album, paid media — each owning its own media
and interaction chrome.
**Key types:** `AlbumContent` (…/AlbumContent.cs) — grouped-media layout; `PhotoContent`/`VideoContent`
— the most-instantiated kinds; `WebPageContent`/`InstantContent` — link preview vs instant view;
`PollContent`/`ChecklistContent` — interactive stateful content; `StickerContent`/`DiceContent` —
Lottie-backed.
**Entry points:** instantiated only by `MessageBubble.UpdateMessageContentControl`; never constructed
elsewhere.
**Traps:** every implementation must answer `IsValid(MessageContent, bool primary)` correctly — that,
not a type check, is what the recycle pool uses to decide reuse eligibility, and what lets `MessageBubble`
skip rebuilding when scrolling reuses a control for a same-kind message.

## Service (system) messages — Telegram/Controls/Messages/MessageService.cs, MessageServiceText.cs, Service/ (22 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Messages/MessageService.cs,Telegram/Controls/Messages/MessageServiceText.cs,Telegram/Controls/Messages/Service -->
Non-regular chat events (member joins, pinned message, gift, background change, group call) as centered
text or a small themed card, outside the bubble pipeline.
**Key types:** `MessageService` — base Button-styled container, text and reactions; `MessageServiceText`
— the static `MessageContent`→`FormattedText` switch shared by chat list previews, reply headers and
automation peers; `MessageGiftContent`, `MessageUpgradedGiftContent`, `MessageChatSetBackgroundContent`,
`MessageHeaderMessageTopicContent` (Telegram/Controls/Messages/Service/).
**Traps:** `MessageService.Recycle()` must run before container reuse — it clears the text block and
drops `_message`. Subclass `UpdateContent` overrides must reset their own conditional template state;
a recycled container of the same subclass inherits whatever was not reset.

## Rich text rendering — Telegram/Controls/FormattedTextBlock.cs, FormattedTextBox.cs, Messages/MessageTextBlock.cs
<!-- map: verified=95560d9f7 paths=Telegram/Controls/FormattedTextBlock.cs,Telegram/Controls/FormattedTextBlock.Selectable.cs,Telegram/Controls/FormattedTextBox.cs,Telegram/Controls/Messages/MessageTextBlock.cs -->
A `RichTextBlock`-based control laying out `StyledText`/entities (bold, links, spoilers, custom emoji,
quotes, mentions) under heavy allocation discipline. `MessageTextBlock` stacks one or more
`FormattedTextBlock`s per message, splitting only around code and quote paragraphs.
**Key types:** `FormattedTextBlock` (Telegram/Controls/FormattedTextBlock.cs, 3291 lines) — builds
Paragraph/Span/Run/Hyperlink through `XamlDirect`; `FormattedTextBlockRecyclePool` (same file) —
per-`XamlRoot` bounded pools of `IXamlDirectObject` paragraphs, spans, runs and `ProjectedHyperlink`s;
`ProjectedHyperlink` — caches the XamlDirect handle and Inlines property to avoid re-resolving per click;
`MessageTextBlock` — multi-block stacking panel with its own `_blocks`/`_ranges` reuse; `FormattedTextBox`
(…/FormattedTextBox.cs, 2380 lines) — the editable composer counterpart.
**Entry points:** `SetText(clientService, FormattedText)` / `Clear()` is the update/recycle pair callers
must use in step with container recycling.
**Traps:** the pool is a `ConditionalWeakTable<XamlRoot, …>` — one per window, not global. Comments
record measured costs behind specific choices (a `GetXamlDirectObjectProperty` plus Inlines lookup ~18us;
a managed DP set ~7x the XamlDirect cost); do not simplify these back to the projection API without
re-measuring. Pooled hyperlinks and paragraphs come back carrying the previous message's properties and
must be fully reset, never assumed blank. An ms-appx font first in a `FontFamily` chain costs ~1ms per
run in RichEdit.

## Reply, forward, footer and reactions — Telegram/Controls/Messages/ (14 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Messages/MessageReply.xaml.cs,Telegram/Controls/Messages/MessageForwardHeader.xaml.cs,Telegram/Controls/Messages/MessageFooter.xaml.cs,Telegram/Controls/Messages/ReactionsPanel.cs,Telegram/Controls/Messages/ReactionButton.cs,Telegram/Controls/Messages/ReactionsMenuFlyout.xaml.cs,Telegram/Controls/Messages/MessageReferenceBase.cs,Telegram/Controls/Messages/MessageFactCheck.xaml.cs -->
Secondary bubble chrome: quoted reply preview, forwarded-from header, timestamp and read-state footer,
reaction pills and their flyouts, saved-messages tags, summary and fact-check strips.
**Key types:** `MessageReply`/`MessageReplyPattern` — reply preview with generated background pattern;
`MessageReferenceBase` — shared base for reply and forward blocks; `MessageFooter` — timestamp, edited
and pinned marks, read receipts, sized by `MessageBubblePanel` for the overlap logic; `ReactionsPanel`
— diff-based pill layout, implements `IDiffEqualityComparer<MessageReaction>`; `ReactionButton` and its
paid/tag variants.
**Entry points:** owned by `MessageBubble`/`MessageService`, both of which implement `IReactionsDelegate`.
**Traps:** `ReactionsPanel` keeps a `Dictionary<ReactionType, ReactionButton>` and does diffed updates
rather than rebuilding, so reaction identity must stay stable across updates. `MessageBubblePanel`
compares `reactions.Footer` against `footer.DesiredSize` to conditionally `InvalidateMeasure()`, so
`ReactionsPanel.Footer` must be set every measure pass before `Reactions.Measure`.

---

# UI: other surfaces

## Stories — Telegram/Controls/Stories/ (17 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Stories -->
The chat-list stories strip and the full-screen viewer, including live-story and channel variants.
**Key types:** `StoriesStrip` (…/StoriesStrip.xaml.cs) — avatar strip bound to `StoryListViewModel.Items`,
debounces `CollectionChanged` via `EventDebouncer`; `StoriesWindow` — the overlay window; `StoryContent`
— per-story media surface; `StoryInteractionBar` and its channel/live variants; `StoryReactionStream`
— flying-reaction particles.
**Traps:** `StoriesStrip` tracks a `_first`/`_last` visible-index window by hand (`UpdateIndexes`)
rather than relying on virtualization events; that bookkeeping decides which rings get expensive visuals.

## Gallery — Telegram/Controls/Gallery/ (11 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Gallery -->
The swipeable photo/video/document viewer, its transport controls, and picture-in-picture.
**Key types:** `GalleryWindow` — an `OverlayWindow` implementing `IGalleryDelegate`, owning a 3-slot
carousel (`GetElement(direction)` for previous/target/next); `GalleryContent` — one slide;
`GalleryTransportControls`; `GalleryCompactOverlay`.
**Entry points:** opened from bubbles and media cells through the navigation service with a
`GalleryViewModelBase`. Not a ListView — the carousel is a fixed 3-element buffer, not virtualized.
**Traps:** only previous/current/next exist at once; playback state must be torn down and reattached as
the carousel slides, or the player keeps decoding an off-screen item.

## Drawers — sticker, GIF and emoji pickers — Telegram/Controls/Drawers/ (10 files)
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Drawers -->
The below-composer flyouts for stickers, GIFs, custom emoji and message effects, all grid
`ListViewBase`s with animated content.
**Key types:** `StickerDrawer` — implements `IDrawer`, wraps `AnimatedListHandler` (viewport-driven
play/pause) and `ZoomableListHandler` (press-and-hold preview); `EmojiDrawer`, `AnimationDrawer`,
`EffectDrawer` follow the same pattern.
**Traps:** the two handlers must be suspended and resumed in lockstep (`_zoomer.Opening = _handler.Suspend`),
or the grid keeps animating behind an open preview.

## Remaining control groups — Telegram/Controls/{Views,Payments,Gifts,Contacts,Calls,Media,Primitives}
<!-- map: verified=95560d9f7 paths=Telegram/Controls/Views,Telegram/Controls/Payments,Telegram/Controls/Gifts,Telegram/Controls/Contacts,Telegram/Controls/Calls,Telegram/Controls/Media,Telegram/Controls/Primitives -->
`Views/` holds sub-page controls swapped into tab hosts (`ForumView`, `SearchChatsView`,
`SearchPostsTabPage`, `RecentChatsView`, `InteractionsView`). `Payments/` and `Gifts/` hold checkout and
gift input widgets. `Calls/`, despite the name, holds small status widgets (`SignalBars`,
`MediaStateBadge`, `VerificationState`), not the call screen. `Media/` holds composition brushes
(`ChatBackgroundBrush`, `MessageBubbleBrush`, `LinearGaussianBrush`, `PowerSavingBrushBase`).
**Key types:** `ForumView` (…/Views/ForumView.xaml.cs) — reuses `ChatListListViewItem` as its container
(line 247); `SearchChatsView` — multi-section results with per-section container types;
`PowerSavingBrushBase` — base for brushes that stop animating under battery saver; `ScrollBarManager`
(…/Primitives/ScrollBarManager.cs) — a VisualStateManager override forcing scrollbars to NoIndicator.
**Traps:** `ScrollBarManager.GoToStateCore` assumes a fixed parent chain (Grid → Grid → Border →
ScrollViewer) to find the scrollbar owner; a template restructure breaks it with no compile-time signal.
A custom VisualStateManager never sees framework state changes.

---

# Media, theming and platform

## Streams — animated media sources — Telegram/Streams/ (11 files)
<!-- map: verified=95560d9f7 paths=Telegram/Streams -->
The `AnimatedImageSource` hierarchy that lets native video/Lottie/WebP players pull bytes from
different origins. A TDLib `File` becomes a playing animation by being wrapped in one of these and
handed to the native player, which calls back through `SeekCallback`/`ReadCallback`.
**Key types:** `AnimatedImageSource` (Telegram/Streams/AnimatedImageSource.cs) — abstract seek/read/outline
contract; `LocalFileSource` — a fully-local file, no waiting; `RemoteFileSource` — pumps `DownloadFile`
and waits on TDLib file updates; `RemoteFilePrefetch` — sizes the download window from observed
consumption rate; `DelayedFileSource`, `AnimatedEmojiFileSource`, `CustomEmojiFileSource`,
`ReactionFileSource`, `DiceFileSource`; `ParticlesImageSource` — synthetic, non-file.
**Entry points:** native players call `AnimatedImageSourceFactory.Create` via the `CreateFromString`
markup extension, or controls construct a source directly from a `Telegram.Td.Api.File`.
**Traps:** `RemoteFileSource.ReadCallback` blocks the native player thread on a `ManualResetEvent` until
TDLib's file-update thread signals it; `UpdateFile` and `MustWait` share `_stateLock`, and misordering
there reintroduces a documented deadlock (a synchronous `GetFileDownloadedPrefixSize` used to hang
because `Client.Run` serializes dispatch and reply on one thread). `Equals`/`GetHashCode` special-case
`IsUnique` sources to avoid cache collisions.

## Thumbnails and the download path — Telegram/Common/ThumbnailController.cs, HttpServer.cs, MediaHttpServer.cs, VideoPreloader.cs
<!-- map: verified=95560d9f7 paths=Telegram/Common/ThumbnailController.cs,Telegram/Common/HttpServer.cs,Telegram/Common/MediaHttpServer.cs,Telegram/Common/VideoPreloader.cs,Telegram/Common/LocalDatabase.cs,Telegram/Common/NativeFile.cs -->
`ThumbnailController` binds an `ImageBrush` to a generation-tracked async pipeline: `Blur(...)` offloads
to `Direct2D.Shared.DrawBlurred` on a background task, converts to a `SoftwareBitmapSource`, and swaps
it onto the brush only if `_generation` still matches. `Bitmap(...)` does the unblurred equivalent.
`HttpServer`/`MediaHttpServer` serve local and streamed media to consumers that need HTTP semantics
over a TDLib file.
**Traps:** every request must be generation-checked after each `await` — assigning to the brush without
that races a stale decode over a fresh one. `Recycle`/`SetSource` deliberately never mutate a handed-out
source in place; the losing request disposes what it built, after detaching it.

## Theming — Telegram/Common/Theme.cs, ThemeColorizer.cs, ColorsHelper.cs, Colors.cs
<!-- map: verified=95560d9f7 paths=Telegram/Common/Theme.cs,Telegram/Common/ThemeColorizer.cs,Telegram/Common/ColorsHelper.cs,Telegram/Common/Colors.cs -->
`Theme : ResourceDictionary` is the per-window, per-thread theme instance (`[ThreadStatic] Theme.Current`).
Applying a theme resolves the accent/custom/chat-theme layer, then `Update(...)` walks a `ThemeLookup`
and mutates existing brushes in place via `AddOrUpdate<T>` rather than replacing dictionary entries.
**Key types:** `Theme` (Telegram/Common/Theme.cs); `MessageBrushes` (same file) — shared bubble brushes
recolored in place; `ThemeAccent`/`ThemeAccentInfo`; `ThemeColorizer`.
**Traps:** brushes must be mutated (`.Color = …`), never replaced — bubbles bind to the same brush
objects, which is exactly what makes a runtime theme switch repaint everything without walking the
visual tree. `MessageBrushes.CreateDictionary()` gives each element its own wrapper `ResourceDictionary`
because `FrameworkElement.Resources` requires a single owner; never share that wrapper. `Update` swallows
`UnauthorizedAccessException` around resource mutation, for an unresolved post-resume XAML quirk. The
app theme is global, the chat override is per window, and popups need it forwarded.

## Diagnostics and crash reporting — Telegram/Common/{Profiler.cs,WatchDog.cs,Instrumentation.cs,ExceptionSerializer.cs}, Telegram/Logger.cs
<!-- map: verified=95560d9f7 paths=Telegram/Common/Profiler.cs,Telegram/Common/WatchDog.cs,Telegram/Common/Instrumentation.cs,Telegram/Common/ExceptionSerializer.cs,Telegram/Logger.cs,Telegram/Common/MonotonicUnixTime.cs,Telegram/Common/InactivityHelper.cs -->
Logging, crash capture and upload, and profiling that ships in release. `Logger` keeps a 200-line ring
buffer (`Dump()`) and forwards to TDLib's own log. `WatchDog` hooks `NativeUtils.SetFatalErrorCallback`,
`CoreApplication.UnhandledErrorDetected`, `BootStrapper.UnhandledException` and `AppDomain.FirstChanceException`,
serializes through `ExceptionSerializer`, rate-limits with a persistent token bucket (100/hour), writes
`crash.id` and `ErrorReports/*.json`, and uploads to the crash endpoint. `Profiler` is a
`[Conditional("INSTRUMENTATION")]` scoped-timer and tally facility.
**Traps:** `Profiler` compiles out entirely unless `INSTRUMENTATION` is defined — expect no output from a
normal Debug or Release build. `WatchDog` is disabled when `Constants.DEBUG`. Report writing uses a
`_reporting` thread-static re-entrancy guard, since serializing a report can itself throw.

## Common helpers, grouped — Telegram/Common/ (110 files)
<!-- map: verified=01cb6feb8 paths=Telegram/Common -->
The cross-cutting helper dump, by cluster. **Extensions and text:** `Extensions*.cs`,
`NormalizingStringBuilder`, `UniqueList`, `MathEx`/`MathFEx`. **Emoji and text rendering:** `Emoji.cs`,
`AutocompleteEntityFinder.cs`, `TextStyleRun.cs`, `TextSelectionCoordinator`/`TextSelectionManager`,
`MarkdownToInstantView.cs`, `PageBlockRenderer.cs`. **Files and downloads:** `UpdateManager.cs` — the
file-id→subscriber bus every source and control uses for TDLib file updates, fed by
`ClientService.UpdateFile` and owning both the weak subscription tables and the per-UI-thread queues
that carry the updates — plus `TdThroughput.cs`, `UriEx.cs`. **Recording:** `Recording/ChatRecordEngine.cs`, `ChatRecordSession.cs`, `AudioWaveform.cs`,
`VoiceSink.cs`. **Chat actions:** `Chats/InputChatActionManager.cs`, `OutputChatActionManager.cs`.
**Visual utilities:** `Direct2D.cs`, `VisualUtilities*.cs`, `CompositionPathParser.cs`, `AlbumLayout.cs`,
`AnimatedListHandler.cs`, `ZoomableListHandler.cs`, `FluidGridView.cs`. **Platform shims:** `SystemTray.*.cs`,
`NotifyIcon.Win32.cs`, `PasskeyManager.*.cs`, `WebAuthn.Win32.cs`, `MediaDevice*.cs`, `Interop.cs`.
**Navigation:** `NavigationService.cs`, `TLNavigationService.cs`, `TLRootNavigationService.cs`.
**App glue:** `ApiInfo.cs`, `PowerSavingPolicy.cs`, `SoundEffects.cs`, `Toast.cs`, `Locale.cs`,
`PhoneNumber.cs`, `CurrencyNumberFormatter.cs`.
**Traps:** `UpdateManager`'s subscription token packs session id and file id into one `long` with reserved
high bits (`CompletionOnly = 1L << 62`); a session that exhausted the file-id budget would collide with
another session's tokens, per its own comment. A UI subscriber is not called when its update is
published but on the next dispatcher hop, by which time the `File` holds whatever TDLib has since
written into it — that is what makes collapsing a burst free, and it means a handler must read the
object it is handed rather than assume it describes the update that woke it. Subscribers that are
neither a `FrameworkElement` nor a `ViewModelBase` with a dispatcher are still called inline on the
TDLib thread, which `RemoteFileSource` depends on. The `subscriber` handed to `Subscribe` is the weak
anchor that decides when the subscription dies, not necessarily the handler's own target — the
`Telegram/Streams` sources pass the control while the handler belongs elsewhere — and `Unsubscribe`
must be given that same anchor or it removes nothing, zeroes the token and orphans the subscription
for good, which `DelayedFileSource.Complete` does today. `Subscribe` takes the drain of the calling
thread rather than reading the subscriber's dispatcher, so it assumes a control subscribes from its
own thread; a wrong guess costs one hop and corrects itself on the first delivery.

## Charts — Telegram/Charts/ (31 files)
<!-- map: verified=95560d9f7 paths=Telegram/Charts -->
Custom chart rendering for the statistics pages, on Composition/Direct2D rather than a charting library.
**Key types:** `BaseChartView` (Telegram/Charts/BaseChartView.cs) — base for the line, bar, pie, step,
stacked and double variants; `ChartData` (Telegram/Charts/Data/) — parsed `StatisticalGraph` data;
`Animator` plus the interpolators — chart zoom/pan and value transitions; `SegmentTree` — range min/max
over a data window; `ChartHeaderView` — the date-range and zoom header.
**Entry points:** the statistics view models feed TDLib `StatisticalGraph` results into `ChartData` and
host a `BaseChartView` subclass in XAML.

## Text recognition, composition, converters, selectors, shims — Telegram/{AI,Composition,Converters,Selectors,Shims}/ (47 files)
<!-- map: verified=95560d9f7 paths=Telegram/AI,Telegram/Composition,Telegram/Converters,Telegram/Selectors,Telegram/Shims -->
`AI/` is OCR selection support for text recognized in images — `RecognizedTextBlock` groups lines into
selectable polygons, `RecognizedTextSpatialIndex` hit-tests, `RecognizedTextSelectionManager` implements
drag selection. Despite the folder name this is computer-vision glue, not an LLM integration.
`Composition/` wraps low-level compositor work XAML cannot express: `CompositionBlobVisual`/
`CompositionVoiceBlobVisual`, the `CompositionDustVisual` particle effects, `CompositionCurveVisual`,
the `Composition*ColorSource` family, `CompositionVSync`. `Converters/` and `Selectors/` are ordinary
XAML `IValueConverter`s and `DataTemplateSelector`s. `Shims/` patches gaps between UWP and the Win32
host (clipboard, focus manager, incremental loading, file pickers).
**Traps:** `Shims/` exists because the Win32 island host lacks some UWP APIs the packaged build uses —
check for Win32 vs UWP conditional compilation before assuming a shim is universal.

---

# Native

## Drawing surfaces — Direct2D over the shared Composition device — Telegram.Native/ (14 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native/Direct2DDevice.cpp,Telegram.Native/Direct2DDevice.h,Telegram.Native/Direct2DDevice.idl,Telegram.Native/SurfaceImage.idl,Telegram.Native/FreeformGradientSurface.cpp,Telegram.Native/ChatBackgroundPattern.cpp,Telegram.Native/MessageBubbleNineGrid.cpp,Telegram.Native/RichMathSurface.cpp,Telegram.Native/Composition,Telegram.Native/Highlight -->
All off-thread D2D rasterization — text layout and measurement, chat backgrounds, gradients, particles,
math formulas, nine-grid bubble masks, syntax highlighting — plus the DirectComposition plumbing XAML has
no API for. Native because XAML composition visuals cannot do arbitrary D2D drawing or thread-safe
rasterization. **A C#-only search misses this entire layer.**
**Key types:** `Direct2DDevice` (Telegram.Native/Direct2DDevice.idl) — owns the app-wide
`CompositionGraphicsDevice`, plus text metrics, WebP and blur; `ChatBackgroundPattern` — SVG pattern onto
an `ICompositionSurface`; `FreeformGradientSurface` — animated mesh-gradient brush, listens for
`RenderingDeviceReplaced`; `MessageBubbleNineGrid` — bubble tail brush and mask; `RichMathSurface` —
MicroTeX formula rasterizer; `CompositionDevice`/`DirectRectangleClip2`/`WindowVisual`
(Telegram.Native/Composition/) — DComp interop for rounded clips and per-window capture.
**Entry points:** `Telegram/Common/Direct2D.cs`, `Telegram/Controls/Chats/ChatBackgroundPresenter.cs`,
`Telegram/Controls/Media/ChatBackgroundBrush.cs`, `MessageBubbleBrush.cs`, `Telegram/Controls/RichMathImage.cs`,
`Telegram/Controls/FormattedTextBlock.cs`.
**Traps:** `Direct2DDevice.Device` is the single shared `CompositionGraphicsDevice`; surfaces subscribe to
its `RenderingDeviceReplaced` for device-lost recovery rather than owning a device.

## Media decode and audio — Telegram.Native/ (16 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native/VideoAnimation.cpp,Telegram.Native/CachedVideoAnimation.cpp,Telegram.Native/VideoAnimationStreamSource.cpp,Telegram.Native/Media,Telegram.Native/AudioPitchEffect.cpp,Telegram.Native/Opus -->
Video and audio decode and playback. Native because it links ffmpeg (libavformat/libavcodec/libswscale/
libyuv) and libVLC directly, and runs decode loops on dedicated threads feeding XAML swap chains.
**Key types:** `VideoAnimation` (Telegram.Native/VideoAnimation.idl) — ffmpeg frame decoder for GIF and
video stickers; `CachedVideoAnimation` — thread-pooled cache around it; `AsyncMediaPlayer`
(Telegram.Native/Media/AsyncMediaPlayer.idl) — libVLC player driving a `CompositionSwapChain`;
`AudioPitchEffect` — `IBasicAudioEffect` for speed/pitch correction; `OpusOutput` (Telegram.Native/Opus/)
— voice-note encode.
**Entry points:** `Telegram/Controls/AnimatedImage.cs`, `Telegram/Controls/NativeVideoPlayer.xaml.cs`,
`Telegram/Services/PlaybackService.cs`, `Telegram/Streams/RemoteFileSource.cs`.
**Traps:** ffmpeg is a vcpkg build patched down to the specific decoders and hwaccels this app needs
(`Libraries/vcpkg-ports/ffmpeg/portfile.cmake`) — not a stock build. ffmpeg is bundled twice; see
`notes/duplicated-libraries.md`.

## OCR and language identification — Telegram.Native/AI/, LanguageIdentification.* (9 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native/AI,Telegram.Native/LanguageIdentification.cpp,Telegram.Native/LanguageIdentification.h,Telegram.Native/LanguageIdentification.idl -->
On-device text recognition from images and language ID for translation, over vendored Google
`libtextclassifier` models.
**Key types:** `TextRecognizer`/`ITextRecognizer` (Telegram.Native/AI/TextRecognizer.idl) — `GetDefault()`/
`GetOne(modelKey)` returning `RecognizedText`/`RecognizedLine`/`RecognizedWord`; `LanguageIdentification`
— static `IdentifyLanguage(text)`.
**Entry points:** `Telegram/Services/TextRecognitionService.cs` and `Telegram/Controls/Gallery/GalleryContent.xaml.cs`;
`Telegram/Services/TranslateService.cs` and `Telegram/ViewModels/DialogViewModel.Translate.cs`.

## Platform glue — Telegram.Native/ (~24 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native/NativeUtils.idl,Telegram.Native/FatalError.cpp,Telegram.Native/GarbageCollectionMonitor.cpp,Telegram.Native/HttpProxyWatcher.cpp,Telegram.Native/ScreenshotManager.idl,Telegram.Native/QrBuffer.idl,Telegram.Native/OrphanTerminator.cpp,Telegram.Native/TelegramWebviewProxy.idl,Telegram.Native/Controls,Telegram.Native/InternalsRT,Telegram.Native/Helpers -->
Win32-only capability C#/WinRT cannot reach: crash capture, file and OS queries, screen capture, webview
bridging, low-level input and drag helpers.
**Key types:** `FatalError` (Telegram.Native/FatalError.idl) — native crash record with stack frames;
`NativeUtils` — file and directory operations, and the shared `TextStyle`/`TextDirectionality` enums used
across text rendering; `ScreenshotManager` — desktop capture to an `ImageSource`; `TelegramWebviewProxy`
— `[allowforweb]` bridge for embedded webviews; `AutomaticDragHelper`/`ControlEx` (Telegram.Native/Controls/);
`LibraryHelper` (Telegram.Native/Helpers/) — obfuscated `LoadLibrary`/`GetProcAddress`.

## VoIP: 1:1 calls — Telegram.Native.Calls/VoipManager.* and friends (~14 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native.Calls/VoipManager.cpp,Telegram.Native.Calls/VoipManager.h,Telegram.Native.Calls/VoipManager.idl,Telegram.Native.Calls/VoipCallProtocol.cpp,Telegram.Native.Calls/VoipDescriptor.cpp,Telegram.Native.Calls/VoipVideoCapture.idl,Telegram.Native.Calls/VoipVideoOutputSink.idl -->
Wraps one tgcalls `Instance` per 1:1 call. Native because tgcalls (with bundled webrtc) is C++-only and
its callbacks arrive on tgcalls worker threads.
**Key types:** `VoipManager` (Telegram.Native.Calls/VoipManager.idl) — owns the `tgcalls::Instance`,
exposes `Start`/`Stop`/`ReceiveSignalingData`/`SetVideoCapture`; `VoipCallProtocol` — the supported
versions from `tgcalls::Meta::Versions()`; `VoipDescriptor` — call setup parameters.
**Entry points:** `Telegram/Services/Calls/VoipCall.cs`, `VoipCoordinator.cs`.
**Traps:** `m_signalingDataEmitted` is written on the UI thread and read on a tgcalls thread and must stay
under `m_signalingLock`, but the WinRT `winrt::event` members deliberately have no additional lock —
adding one reintroduces a deadlock, because tgcalls-thread callbacks reach managed code that takes locks
while the UI thread unsubscribes under those same locks. See `notes/voip-review.md`.

## VoIP: group calls and livestreams — Telegram.Native.Calls/VoipGroupManager.* and friends (~28 files)
<!-- map: verified=95560d9f7 paths=Telegram.Native.Calls/VoipGroupManager.cpp,Telegram.Native.Calls/VoipGroupManager.h,Telegram.Native.Calls/VoipGroupManager.idl,Telegram.Native.Calls/VoipGroupDescriptor.cpp,Telegram.Native.Calls/VoipScreenCapture.cpp,Telegram.Native.Calls/VoipLoopbackCapture.cpp,Telegram.Native.Calls/MediaChannelDescriptionsRequestedEventArgs.cpp,Telegram.Native.Calls/BroadcastPartRequestedEventArgs.cpp -->
Wraps tgcalls `GroupInstanceCustomImpl`: SFU-style multi-participant audio and video, broadcast-part
streaming for large livestreams, and screen share.
**Key types:** `VoipGroupManager` — owns the group instance; encryption of group payloads is delegated
back to C# through `EncryptGroupCallDataDelegate`/`DecryptGroupCallDataDelegate` (byte arrays, because the
managed side hands the bytes straight to TDLib, which wants a `byte[]` to base64); `BroadcastPartTaskImpl`/
`RequestMediaChannelDescriptionTaskImpl` (VoipGroupManager.h); `VoipVideoCapture`/`VoipScreenCapture`.
**Entry points:** `Telegram/Services/Calls/VoipGroupCall.cs`.
**Traps:** callbacks run on tgcalls and webrtc internal threads under `rtc_base` mutexes — the same
UI/tgcalls split as 1:1. Leaving a call once deadlocked TDLib's dispatch thread against two tgcalls
threads; see the group-call teardown work in `notes/`.

## Libraries — submodules, vendored source and prebuilt binaries — Libraries/
<!-- map: verified=95560d9f7 paths=Libraries -->
**Submodules** (per `.gitmodules`): `tdlib`, `tgcalls`, `libwebp`, `libprisma` (syntax highlighting behind
Telegram.Native/Highlight), `MicroTeX` (`heads/tdesktop` branch, behind `RichMathSurface`), `flatbuffers`,
`libutf`, `CoreWindowCustomDPI`. **Vendored source:** `libtextclassifier`. **Prebuilt binaries checked in
or downloaded:** `rlottie` (`RLottie.dll`/`.winmd`/`.lib` per arch — consumed directly by
`Telegram/Controls/AnimatedImage.cs`, not through Telegram.Native; built from the separate
`C:\Source\RLottie.UWP` repo), `wallet-engine`, `tdjson` (local CMake tree providing `td_api.tl`),
`ton-walletkit`, `unigram-iv-editor` (the JS instant-view host). **vcpkg overlay ports:**
`Libraries/vcpkg-ports/{ffmpeg,libvlc,webrtc}` — ffmpeg, libVLC and webrtc come in as vcpkg packages with
local patches.
**Traps:** the ffmpeg portfile is hand-patched to disable most codecs and enable only D3D11VA/DXVA2
hwaccel plus a narrow allowlist; building against upstream vcpkg ffmpeg changes decoder availability.

---

# Build and packaging

## Build orchestration — Build.ps1, Build.Modern.ps1, Build.Win32.ps1, UpdateManifest.ps1
<!-- map: verified=95560d9f7 paths=Build.ps1,Build.Modern.ps1,Build.Win32.ps1,UpdateManifest.ps1 -->
Three PowerShell drivers, one per flavour, each locating VS's own MSBuild through `vswhere.exe` (never
the SDK MSBuild — the UWP XAML compiler hooks live only in VS's ImportBefore/ImportAfter) and building
through the matching `.slnx`, never the project file.
**Key files:** `Build.ps1` — thin: `UpdateManifest.ps1` plus the `Telegram_Msix` target; `Build.Modern.ps1`
— multi-arch bundle build with warning reporting; `Build.Win32.ps1` — publishes `Telegram.Win32.csproj`
directly, no wapproj; `UpdateManifest.ps1` — stamps version and identity into the manifest from a git
rev-count.
**Entry points:** `.\Build.ps1 -arch x64|arm64 -mode SideloadOnly`;
`.\Build.Modern.ps1 -Platform x64,ARM64 -Mode SideloadOnly|StoreUpload -Identity Alternative|Original`;
`.\Build.Win32.ps1 -Platform x64 -Identity Bundle|Registered`.
**Traps:** all three need vswhere on PATH before ILC runs — ILC shells out to it looking for link.exe, and
a missing one gives a misleading MSB3073. The native vcxprojs still use packages.config, so `-restore`
alone silently skips them; every driver also passes `-p:RestorePackagesConfig=true`. `Build.Modern.ps1`
deliberately never calls `UpdateManifest.ps1`. ARM64 for Win32 is explicitly untested.

## The three csproj flavours — Telegram/Telegram.csproj, Telegram.Modern.csproj, Telegram.Win32.csproj
<!-- map: verified=95560d9f7 paths=Telegram/Telegram.csproj,Telegram/Telegram.Modern.csproj,Telegram/Telegram.Win32.csproj -->
The same sources (`RootNamespace`/`AssemblyName` = `Telegram` in all three, separate obj/bin roots) through
three different project systems.
- **`Telegram.csproj`** — legacy non-SDK UWP, `TargetPlatformVersion=10.0.26100.0`, .NET Native in Release,
  `AppContainerExe`, 4804 lines. **Not globbed:** measured 1299 `<Compile Include>` and 474 `<Page Include>`
  entries — a new file is invisible to this build unless added by hand. Packaged by `Telegram.Msix.wapproj`.
- **`Telegram.Modern.csproj`** — SDK-style (importing `Sdk.props` by hand, not the `Sdk=` attribute, so the
  output paths can be set first), `net10.0-windows10.0.26100.0`, `UseUwp=true`, `PublishAot=true` outside
  Debug, `WinExe`. **Globbed:** zero `<Compile Include>` entries, with ~8 `<Compile Remove>` lines
  (`Host\**`, `**\*.Win32.cs`, a few one-offs). Ships through `Telegram.Msix.Modern.wapproj`.
- **`Telegram.Win32.csproj`** — same SDK-style base, but `Exe` with `StartupObject=Telegram.Host.Program`
  (XAML Islands desktop host), `Compile Remove="**\*.Uwp.cs"` — the opposite fork. Packages itself; there
  is no wapproj for it.
**Traps:** the Modern/Win32 comment states parity with the classic project's file list is "today" only and
unverified by tooling, so silent drift is possible. `DisableRuntimeMarshalling=true` is set only on Modern
and Win32 (every DllImport has a LibraryImport counterpart); the classic project omits it deliberately,
since .NET Native keeps the runtime-marshalling branches. All three share
`PackageCertificateKeyFile=..\Telegram_TemporaryKey.pfx`.

## Native dependencies and vcpkg — Directory.Build.props, Directory.Build.targets, vcpkg.json, vcpkg-configuration.json
<!-- map: verified=95560d9f7 paths=Directory.Build.props,Directory.Build.targets,vcpkg.json,vcpkg-configuration.json -->
Repo-root imports auto-included by every project, supplying the native builds and the app projects with
vcpkg manifest-mode dependencies.
**Key files:** `vcpkg.json` — manifest mode, `builtin-baseline: c3867e714dd3a51c272826eea77267876517ed99`;
dependencies boost-regex, ffmpeg (dav1d/opus/vpx), libogg, libvlc, libyuv, lz4, nu-book-zxing-cpp, openssl,
opus, webrtc, zlib. `vcpkg-configuration.json` — overlay port at `./Libraries/vcpkg-ports`.
`Directory.Build.props` — resolves `VcpkgRoot` (explicit → `VCPKG_ROOT` → sibling `..\vcpkg` → VS's bundled
component), sets the triplet (`x64-uwp`/`arm64-uwp`), disables autolink and applocal.
`Directory.Build.targets` — per-project `AdditionalDependencies` lib lists (autolink is off, so linking is
explicit), a baseline-ancestor check against the live vcpkg checkout, and the targets that copy vcpkg
runtime DLLs and flatten libvlc's plugin tree into package Content.
**Traps:** only `Telegram.Native`, `Telegram.Native.Calls` and the three app projects restore vcpkg.
openssl and zlib are excluded from the copied runtime DLLs to avoid clashing with tdjson's own OpenSSL
under the same filename. `VcpkgApplocalDeps=false` means DLLs can go missing from the appx on an
incremental build where the linker did not run.

## Solutions — Telegram.slnx, Telegram.Modern.slnx, Telegram.Win32.slnx
<!-- map: verified=95560d9f7 paths=Telegram.slnx,Telegram.Modern.slnx,Telegram.Win32.slnx -->
XML `.slnx`, not classic `.sln`; one per flavour, each wiring its csproj/wapproj plus the shared native
vcxprojs and the generators. The Win32 one has no wapproj and no Telegram.Stub — it packages itself.
Modern has its own solution specifically so a plain `Telegram.slnx` build does not pay for a NativeAOT
compile.
**Traps:** building the project file instead of the solution leaves `$(SolutionDir)` empty, so the native
projects write their `.winmd`/`.dll` to the wrong path — a stale projection, not a build error.

## Packaging and signing — Telegram.Msix/, Telegram.Msix.Modern/, Package*.appxmanifest, the Win32 XSLT transforms
<!-- map: verified=95560d9f7 paths=Telegram.Msix,Telegram.Msix.Modern,Telegram/Package.appxmanifest,Telegram/Package.StoreAssociation.xml,Telegram/Package.Win32.xslt,Telegram/Package.Win32.Final.xslt -->
Each flavour has its own package identity, all signed with the single repo certificate.
**Key files:** `Telegram.Msix/Telegram.Msix.wapproj` — packages the classic csproj plus Telegram.Stub;
`Telegram.Msix.Modern/Telegram.Msix.Modern.wapproj` — packages the Modern csproj plus the Stub, and takes
the classic manifest as a copy patched by `XmlPoke` rather than maintaining a second one;
`Telegram/Package.appxmanifest` — the one hand-maintained manifest (capabilities, extensions, file
associations), rewritten per config by `UpdateManifest.ps1`; `Telegram/Package.Win32.xslt` — a deletion
pass (XmlPoke cannot delete nodes) stripping the app-service extension and UWP-only capabilities and adding
`runFullTrust`; `Telegram/Package.Win32.Final.xslt` — applied to the *generated* AppxManifest, injecting the
`Microsoft.VCLibs.140.00` dependency, without which Telegram.Native fails to load with a misleading
`REGDB_E_CLASSNOTREG`. Certificate: `Telegram_TemporaryKey.pfx` at the repo root — the only key.
**Traps:** four distinct package identities exist because Windows cannot move an installed app between a
registered loose layout and a bundle; sharing an identity blocks reinstall. `StoreUpload` with
`-Identity Alternative` produces a `.msixupload` the Store will reject — the driver warns but does not
block. Never uninstall to update a dev package: `Remove-AppxPackage` deletes LocalState, and with it the
login.

## Telegram.Stub — Telegram.Stub/ (11 files)
<!-- map: verified=95560d9f7 paths=Telegram.Stub -->
A small NativeAOT WinExe: the full-trust process the packaged manifests point their
`windows.fullTrustProcess` and app-service extensions at (tray icon, passkeys bridge). Referenced by both
wapprojs; absent from Win32, which is already full trust.
**Key files:** `Telegram.Stub/Telegram.Stub.csproj` — `EnableDefaultItems=false` with 6 explicit Compile
entries, `PublishAot`, `InvariantGlobalization`, `DisableRuntimeMarshalling`; links `NotifyIcon.Win32.cs`
and `WebAuthn.Win32.cs` out of `Telegram\Common\` by relative path, shared with the Win32 flavour.
**Traps:** `DisableRuntimeMarshalling` must stay in sync with the app project it shares interop code with,
or that interop compiles clean and throws.

## Package sources and line endings — nuget.config, .gitattributes
<!-- map: verified=95560d9f7 paths=nuget.config,.gitattributes -->
`nuget.config` clears the feed list and keeps only nuget.org plus a local `Telegram@Local` feed pointing at
`Libraries`. `.gitattributes` is global `text=auto`, but forces LF on `Telegram/Strings/en/Resources.xml`
and everything under `Libraries/vcpkg-ports/**`, and marks `Libraries/unigram-iv-editor/editor.html` as
`-text`.
**Traps:** the vcpkg overlay rule is not an oversight — vcpkg applies `.patch` files with `git apply`,
which rejects a CRLF-converted tree.

---

# Resources and tooling

## Localization — Telegram/Strings/ (32 languages), Telegram/Services/LocaleService.cs
<!-- map: verified=95560d9f7 paths=Telegram/Strings,Telegram/Services/LocaleService.cs -->
All user-facing strings. `en` is the source of truth; every other locale is a translation carrying only a
`Resources.resw`, arriving through its own pipeline.
**Key files:** `Telegram/Strings/en/Resources.xml` — **the only file to edit by hand**;
`Telegram/Strings/en/Resources.resw` and `Resources.cs` — **generated**, tracked, so they do show up in
diffs after the generator runs; `Telegram/Services/LocaleService.cs` — the runtime resolver,
`GetString(key)` / `GetString(key, quantity)`, applying CLDR plural rules.
**Entry points:** app code calls `Strings.Xxx` or `Strings.GetString(Strings.R.Xxx)`; plurals go through
`Locale.Declension(Strings.R.<Name>, count)`, which picks the `_one`/`_other` entry by CLDR rule, not by
an English singular/plural test.
**Traps:** never add a string by adding a property to `Resources.cs`. The generator lives outside this
repo (`C:\Source\UnigramUtils\SynchronizeResources`) and Fela runs it, so a new string does not compile
until then — expected, not a mistake to work around. Wording is taken from the Android app wherever it
exists. `.gitattributes` pins `Resources.xml` to LF: the one deliberate exception to the CRLF rule. Never
pass several `Strings.X` values to a picker method — every one gets realized.

## Themes and generated defaults — Telegram/Themes/ (13 files), Tools/ThemeDefaults/
<!-- map: verified=95560d9f7 paths=Telegram/Themes,Tools/ThemeDefaults,Telegram/Services/Theme/ThemeDefaults.g.cs,Telegram/Services/Theme/ThemeDefaults.cs -->
The app's non-per-control XAML resources, plus the generator that pins framework brush defaults so a
runtime theme switch can recolor them in place.
**Key files:** `Telegram/Themes/Generic.xaml` (9381 lines) — its first 63 lines are pure
`MergedDictionaries` pulling per-control XAML out of `Controls/**`; styles proper start at line 65;
`Telegram/Themes/Accent.xaml` — `ThemeDictionaries` (Light/Dark) of brush and color keys;
`Telegram/Themes/Messages.xaml` — message-list templates and selectors; `Tools/ThemeDefaults/light.tsv`,
`dark.tsv` — one row per pinned framework brush; `Telegram/Services/Theme/ThemeDefaults.g.cs` — the packed
generated table.
**Entry points:** `py -m themedefaults <command>` from `Tools/ThemeDefaults` regenerates
`ThemeDefaults.g.cs` from the tsv tables.
**Traps:** generation is a one-off — the framework rows are frozen and checked in; only the ~38 `custom`
rows still change. **Never prune a row because it matches the framework default**: identity is what makes
a runtime theme switch repaint, so a "redundant" row is a latent bug that only shows as a stale color
after a switch.

## Icon font — Tools/IconFont/ (SVG to Telegram.ttf)
<!-- map: verified=95560d9f7 paths=Tools/IconFont,Telegram/Assets/Fonts -->
Builds `Telegram/Assets/Fonts/Telegram.ttf` from `icons.json` plus the SVGs in `icons/`. Replaced the
IcoMoon workflow.
**Key files:** `Tools/IconFont/README.md` — the authoritative command reference; `Tools/IconFont/icons.json`
— the manifest (name, codepoint, source: a local SVG or a `fluent:` live source);
`Tools/IconFont/iconfont/{fontbuild,manifest,outline}.py`; `Tools/IconFont/identified.txt` — the record
resolving nameless `uniXXXX` glyphs; `notes/icon-font.md` — the project notes.
**Entry points:** `py -m iconfont <command>` from `Tools/IconFont/` — `build`, `check` (manifest against
`Icons.cs` and XAML), `verify`/`changes`, `update`/`adopt`/`drift` (sync with upstream Fluent),
`identify`/`rename`/`tidy`.
**Traps:** metrics are frozen at IcoMoon's values (1024 units/em, ascender 960, descender 64) and every
glyph position depends on them. Codepoints are append-only: 763 raw `&#xE9F1;`-style literals across 211
XAML files reference them directly, and `App.xaml` points `TelegramThemeFontFamily`/`SymbolThemeFontFamily`
at this font — reshuffling one silently changes icons app-wide, and a missing codepoint renders nothing,
with no fallback. `build` overwrites the ttf in place; keep a `git show HEAD:…` copy first.

## Assets — Telegram/Assets/ (53 generated .cs plus media)
<!-- map: verified=95560d9f7 paths=Telegram/Assets -->
Animations, audio, images, logos, toast payloads and tray icons, plus 53 generated animated-icon sources.
**Key files:** `Telegram/Assets/Icons/*.cs` — LottieGen-generated `IAnimatedVisualSource` classes from the
sibling `.json` Lottie files; `Telegram/Assets/Fonts/Telegram.ttf`; `Animations/`, `Toasts/`, `Logos/`,
`Mockup/`, `JumpList/`.
**Traps:** each `Assets/Icons/*.cs` header names the exact LottieGen invocation that produced it
(`-GenerateColorBindings -Language CSharp … -Namespace Telegram.Assets.Icons`) — regenerate, do not
hand-edit.

## Benchmarks — Telegram.Benchmarks/ (28 files), Telegram.Benchmarks.NetNative/
<!-- map: verified=95560d9f7 paths=Telegram.Benchmarks,Telegram.Benchmarks.NetNative -->
A standalone harness (not in the solutions) measuring the TDLib JSON parse path across three hosts, so
parser and runtime tradeoffs are decided on numbers.
**Key files:** `Telegram.Benchmarks/README.md` — methodology, results and resume log; read "Where this
stands" first; `Suite.cs` — the shared suite all three hosts run; `Harness.cs` — the hand-rolled `--plain`
harness, since BenchmarkDotNet cannot run under AOT; `Corpus.cs` and `Corpus/*.jsonl`;
`Uwp/UwpHost.cs` — the UWP/.NET 10/NativeAOT host; `Telegram.Benchmarks.NetNative/Stage.ps1` — loose-file
staging for the .NET Native host.
**Entry points:** `dotnet run -f net10.0 -c Release -- --validate-only|--plain|--filter "*"` on the
desktop; publish plus `Stage.ps1` plus `Add-AppxPackage` for the UWP AOT host.
**Traps:** three hosts exist because the shipping app runs .NET Native, which resolves a different
(netstandard2.0) System.Text.Json/System.Memory asset than desktop .NET 10 — results differ by ~3x and
can even reverse between hosts. Compare within a run, never across runs or hosts.

---

# Existing deep dives — notes/

Investigations, reviews and open todo lists. Several are status-tagged in their own first lines ("landed
on develop", "Done", "analysis only") — read that line before treating one as open work.

- `binding-audit.md` — classic `{Binding}` under CsWinRT/NativeAOT; closed a Phase-4 net10-port item.
- `chat-history-scroll-mode-todo.md` — `ChatHistoryView` ItemsUpdatingScrollMode across writers, readers and `SynchronizedList`.
- `clientservice-review.md` — read-through of the seven `ClientService.*.cs` partials and the topic services.
- `dependencies-todo.md` — native dependency state: vcpkg manifest mode, libvlc and webrtc as prebuilt downloads.
- `duplicated-libraries.md` — libraries shipped more than once (ffmpeg, ~25MB).
- `formatted-text-block-review.md` — landed on develop; 24 of 27 items fixed, 3 open decisions.
- `formatted-text-block-test-plan.md` — test plan for the formatted-text-block review branches.
- `formatted-text-block-test-messages.py` — the companion test-message generator.
- `formatted-text-box-review.md` — FormattedTextBox/ChatTextBox after the typing and paste perf work.
- `icon-font.md` — the IcoMoon replacement: manifest stats and working commands.
- `message-collection-plan.md` — `MessageCollection`: live list plus throwaway slice buffer.
- `net10-benefits-and-winui3.md` — what .NET 10 unlocks, and whether WinUI 3 follows.
- `net10-port-todo.md` — porting to .NET 10 while keeping .NET Native alive. Resume point.
- `package-gates.md` — the msixbundle build gates and why each exists.
- `passcode-encryption.md` — making the passcode actually encrypt; today it is a UI-only gate.
- `pending-messages-review.md` — streamed bot `updatePendingMessage` handling; all applied.
- `sendfiles-popup-todo.md` — `SendFilesPopup` across storage entities and album grouping. Resume point.
- `settings-service-refactor.md` — `SettingsService.Current` vs per-session instances; analysis only.
- `swipe-to-go-back.md` — the Chrome-style back gesture for `MasterDetailView`.
- `tdlib-vector-migration.md` — done: TDLib `vector<x>` exposed as immutable `Vector<T>`.
- `tdlib-vector-mutations.md` — every in-place write into a TDLib-sourced list.
- `thumbnail-controller-todo.md` — `ThumbnailController` and `Direct2DDevice::DrawBlurred`.
- `unpackaged-win32.md` — running the Win32 flavour without package identity.
- `voip-review.md` — line-by-line review of `VoipManager`/`VoipGroupManager` against tgcalls.
- `wallet-engine-csharp-todo.md` — C# bindings to the Rust `wallet-engine` TON library.
- `win32-xaml-islands.md` — the third migration path: Win32 host plus UWP XAML islands.
- `xaml-lifecycle.md` — the order of lifecycle callbacks for FrameworkElement, Control, Page and list containers.

Two more reviews live at the repo root rather than in `notes/`: `window-lifetime-review.md`
(WindowContext, ViewService, shutdown) and `layout-cycle-audit.md`.
