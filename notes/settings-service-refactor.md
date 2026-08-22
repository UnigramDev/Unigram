# SettingsService refactor — analysis and plan

Status: analysis only, nothing implemented. Written 2026-08-22 against `develop` @ faf8caf55.

## 1. What is there today

Two entry points already exist, but neither is honest about what it holds.

| | `SettingsService.Current` | `new SettingsService(session)` |
|---|---|---|
| created by | static lazy field | `SessionImpl` ctor (generated resolver) |
| `_container` | `LocalSettings` (root) | `LocalSettings\{n}` |
| `_local` | `LocalSettings` | `LocalSettings` |
| `_own` | **null** | `LocalSettings\{n}` |
| `_container` for session 0 | — | `LocalSettings` (root), see 3.8 |
| `Session` | 0 | n |
| reached as | `SettingsService.Current` (1745 sites) | `ISession.Settings`, `ViewModelBase.Settings`, ctor injection |

Both implement the *same* `ISettingsService`. So every member exists on both objects, and
whether a given property means "the app" or "this account" depends on which of four fields its
accessor happens to name — `_container`, `_local`, `_own` or `_theme`. `_container` is the worst
of them, because it silently means *root* on `Current` and *the account* on a session instance.

Container layout on disk:

```
LocalSettings\                    <- _local, and _container on Current
  ├─ {0}, {1}, ...                <- _own, and _container on a session instance
  │    ├─ AutoDownload
  │    ├─ Video                   (per account, but see 3.2)
  │    └─ PinnedMessages
  ├─ Theme                        <- _theme; AppearanceSettings + MessageFontSize
  │    ├─ ChatThemeLight
  │    └─ ChatThemeDark
  ├─ Diagnostics
  ├─ PasscodeLock
  ├─ VoIP
  ├─ ToolTip
  ├─ Emoji
  └─ Channels                     (written straight from StickerDrawerViewModel)
```

Note that `Stickers`, `Translate` and `Playback` are constructed with `_local`, i.e. their keys
(`SelectedTab`, `IsTranslateEnabled`, `PlaybackRate`, `IsSidebarEnabled`, …) sit **unprefixed in
the root**, mixed in with `SettingsService`'s own root keys. They read like sections but are not.

`Windows.Storage` reaches outside the settings classes in four places: `LifetimeService`
(2 sites), `StickerDrawerViewModel` (1), the `GetBoolean/GetInt32/GetInt64` extensions in
`Common/Extensions.cs`, and `AutoDownloadSettings`, which takes an `ApplicationDataContainer`
directly instead of deriving from `SettingsServiceBase`.

`SettingsLegacyService` is not settings at all — it is an in-memory navigation-state bag used
only by `FrameFacade`. It is misnamed and misfiled; nothing else uses it.

## 2. Inventory

Every member of `SettingsService`, by where its bytes actually land.

### 2.1 Global — root container, correctly declared (`_local`)

`VerbosityLevel`, `DialogsWidthRatio`, `IsSidebarOpen`, `IsAdaptiveWideEnabled`,
`AreSmoothTransitionsEnabled`, `AreCallsAnimated`, `AreMaterialsEnabled`, `IsTrayVisible`,
`IsLaunchMinimized`, `HideArchivedChats`, `IsAccountsSelectorExpanded`, `AccountsSelectorOrder`,
`IsAllAccountsNotifications`, `UseLeftTabsForChats`, `SwipeToShare`, `SwipeToReply`,
`SwipeToGoBack`, `FullScreenGallery`, `UseSystemSpellChecker`, `IsSendByEnterEnabled`, `Pencil`,
`PreviousSession`, `ActiveSession`, `LanguagePackId`, `LanguagePluralId`, `LanguageBaseId`,
`LanguageShownId`.

### 2.2 Global — root container, but declared on `_container`

Physically identical to 2.1 today, only because every reader goes through `Current`. Reading any
of these from a session instance returns the wrong container.

`AutoPlayAnimations`, `AutoPlayVideos`, `AutoPlayStickers`, `AutoPlayStickersInChats`,
`AutoPlayEmoji`, `AutoPlayEmojiInChats`, `IsPowerSavingEnabled`, `IsStreamingEnabled` (21 sites),
`IsDownloadFolderEnabled`, `VolumeLevel`, `ReportsCount`, `ReportsDate`, `AnonymousUserId`,
`InstallBetaUpdates`, `EnabledProxyId`, `MigratedProxy`.

### 2.3 Global — named containers

| section | container | note |
|---|---|---|
| `Appearance` | `Theme` | also owns night mode, a `Timer` and window broadcasts — see 5.4 |
| `MessageFontSize` / `CaptionFontSize` | `Theme` | lives in the appearance container but hangs off `SettingsService` |
| `Diagnostics` | `Diagnostics` | 298 call sites, the single most-used section |
| `PasscodeLock` | `PasscodeLock` | global, yet cleared when *any* account logs out (5.5) |
| `VoIP` | `VoIP` | |
| `ToolTip` | `ToolTip` | |
| `Emoji` | `Emoji` | |
| `Stickers` | root, unprefixed | |
| `Translate` | root, unprefixed | |
| `Playback` | root, unprefixed | |
| `SendLargePhotos` | `Diagnostics` | a `SettingsService` property backed by the diagnostics container |

### 2.4 Per account — `{n}` container

`UserId`, `UseTestDC`, `Chats` (+ `SetChatPinnedMessage`/`GetChatPinnedMessage`),
`Notifications`, `AutoDownload`, `Video`, `UseSystemProxy`, `LastProxyId`,
`IsReplaceEmojiEnabled`, `IsContactsSortedByEpoch`, `IsSecretPreviewsEnabled`,
`UseLeftTabsForForums`, `LastMessageTtl`, `UseLessData`.

### 2.5 Dead

`Version`, `SystemVersion`, `UpdateVersion`, `CleanUp`, `Container` — zero callers outside
`SettingsService` itself. `UpdateVersion` also carries `CurrentVersion = 10.1.0`, stale since the
app is on 12.x.

## 3. Bugs the mixing has already produced

These are not hypothetical; each is reachable from the shipped UI.

### 3.1 `static` backing fields on per-account properties

`UseSystemProxy` and `LastProxyId` read and write `_own`, but cache into a **static** field. The
first account to touch either one publishes its value to every other account for the rest of the
session. `ProxyService.Migrate` reads `settings.UseSystemProxy` per session id — so with two
accounts it migrates the wrong one.

The same static-on-`_container` pattern covers `DistanceUnits`, the six `AutoPlay*`,
`IsPowerSavingEnabled`, `VolumeLevel`, `VolumeMuted`, `ReportsCount`, `ReportsDate`,
`AnonymousUserId`, `InstallBetaUpdates`, `EnabledProxyId` and `MigratedProxy`. Those are harmless
*today* only because every reader happens to use `Current`.

### 3.2 `Video` is a static field built from `_own`

```csharp
private static VideoSettings _video;
public VideoSettings Video => _video ??= new VideoSettings(_own);
```

Whichever account is constructed first owns `LocalSettings\{n}\Video` for the whole process, and
every other account's resume positions are read from and written to it. Also
`SettingsService.Current.Video` would throw, since `_own` is null there.

### 3.3 Split brain — a setting written to one container and read from another

**`DistanceUnits`.** `SettingsAppearanceViewModel` writes `Settings.DistanceUnits` → account
container. `Converters/Formatter.cs` reads `SettingsService.Current.DistanceUnits` → root. The
static cache hides it within a session; on the next launch the reader finds root empty and falls
back to `Automatic`, so the distance unit resets on every restart.

**`VolumeMuted`.** `StoryContent` writes `_viewModel.Settings.VolumeMuted` → account container.
`GalleryTransportControls` and `NativeVideoPlayer` read `SettingsService.Current.VolumeMuted` →
root. Same shape: consistent in-session, diverges after a restart.

Both are scoped by 3.8: for session 0 the account container *is* the root, so neither reproduces
on the first account. They need a second account to be the active one.

### 3.4 Getter and setter naming different containers

`InstallBetaUpdates`, `EnabledProxyId` and `MigratedProxy` read `_container` and write `_local`.
Benign on `Current`, wrong on a session instance — a session write lands in root and is then read
back from `{n}`. `SettingsAdvancedViewModel` reads and writes `InstallBetaUpdates` through the
session, so on any account past the first its toggle shows `true` however it was left. Scoped by
3.8 like the two above.

`UserId`'s asymmetry is deliberate (it also writes the `User{id}` → session index into root), and
should keep working, but wants a comment saying so.

### 3.5 `Current` cannot safely serve half its own interface

`_own` is null on `Current`, so `UseTestDC`, `UserId`'s setter, `AutoDownload`, `Video` and
`SetChatPinnedMessage`/`GetChatPinnedMessage` all dereference null. `Chats` does not throw — it
falls through `SettingsServiceBase(null)` to the root container and writes per-chat scroll state
into `LocalSettings` root. Nothing calls them that way today; the interface invites it.

### 3.6 `Clear()` does not clear the caches

`SettingsService.Clear()` empties the containers but leaves every `??=`-cached field populated,
including the statics. `PasscodeLockSettings.Clear()` is the only one that resets its fields.
After a log-out-and-back-in on the same session id, stale values are served from memory.

### 3.7 `AddOrUpdateValue`'s change detection does not work

```csharp
if (container.Values[key] != value)   // object != object -> reference comparison
```

For every non-string type the operands are freshly boxed, so the comparison is always true: the
write always happens and `valueChanged` is always `true`. The `bool` return is never consumed
anywhere in the app, so this only costs an extra WinRT round trip per write — but it means the
"only write when changed" intent has never held.

### 3.8 Session 0's account container is the root

```csharp
public SettingsService(int session)
    : base(session > 0 ? ...CreateContainer($"{session}", ...) : null)
```

`SettingsServiceBase(null)` falls back to `LocalSettings`, so on session 0 — the single-account
case, and the majority of installs — `_container` is the **root**, not `LocalSettings\0`. `_own`
is `LocalSettings\0` regardless, which is presumably why it was added.

So every `_container` property is app-wide on the first account and per-account on the others.
This is almost certainly deliberate: session 0 predates multi-account, and its settings were
already at root when the second account was introduced. It is also why the bugs in 3.3 and 3.4
have gone unnoticed.

An account's settings therefore cannot simply be repointed at `_own`: for session 0 the existing
values are at root, and moving the code without moving the data loses them. Step 2 does both.

## 4. Target shape

Two entry points, two interfaces, one storage seam.

```
IAppSettings          AppSettings.Current      (process-global, one instance)
IAccountSettings      ISession.Settings        (one per session, constructed with the id)
ISettingsStore        one per container        (the only thing that knows about storage)
```

### 4.1 `ISettingsStore`

```csharp
public interface ISettingsStore
{
    ISettingsStore GetContainer(string name);   // creates on demand
    void DeleteContainer(string name);

    bool ContainsKey(string key);
    void Remove(string key);
    void Clear();

    bool TryGetValue(string key, out bool value);
    bool TryGetValue(string key, out int value);
    bool TryGetValue(string key, out long value);
    bool TryGetValue(string key, out float value);
    bool TryGetValue(string key, out double value);
    bool TryGetValue(string key, out string value);

    void SetValue(string key, bool value);
    void SetValue(string key, int value);
    void SetValue(string key, long value);
    void SetValue(string key, float value);
    void SetValue(string key, double value);
    void SetValue(string key, string value);

    void Flush();                               // no-op on ApplicationData
}
```

Six types is the whole set. Every richer value in the app is already encoded above the store —
`byte[]` as base64, `DateTime` as a file-time `long`, `TimeSpan` as minutes, `Color` as hex,
`Vector2` as two floats, `HashSet<string>` and `int[]` as joined strings. Nothing uses
`ApplicationDataCompositeValue`, `Guid` or `DateTimeOffset` as a stored type.

Overloads rather than `TryGetValue<T>`: the set is closed, it keeps a future non-WinRT backend
boxing-free, and it avoids generic virtual dispatch, which .NET Native handles worst. The
`ApplicationData` implementation boxes anyway — the `IPropertySet` is `object`-typed — so nothing
is lost today.

`Flush()` exists because a file-backed store needs an explicit save point; `ApplicationData`
persists implicitly. Call it on suspend, on close, and after `Clear()`.

The store must be safe for concurrent access. Settings are read off the TDLib update thread today
(`Settings.Notifications.IncludeMutedChats` inside `SessionImpl.Handle`), not only the UI thread.

### 4.2 Layout is data, the object graph is code

The single hardest constraint: **a user's existing settings must keep working.** That means the
refactor may reorganise classes freely but must not move a key to a different container unless it
also migrates it. In particular `Stickers`/`Translate`/`Playback` keep their unprefixed root keys
even though they will read as proper sections — the store lets a section point at the root with
no prefix, so the code can be tidy while the layout stays exactly where it is.

The two split-brain settings (3.3) are the exception: they need one home plus a one-shot promote
(if root has no value and the active account does, copy it up).

There is also a cross-process contract: **`Telegram.Stub` reads `IsLaunchMinimized` straight from
`ApplicationData.Current.LocalSettings`.** Any non-`ApplicationData` backend must either keep
writing that one key where the stub can see it, or move the stub at the same time.

### 4.3 What goes where

`IAppSettings` gets everything in 2.1, 2.2 and 2.3, plus `MessageFontSize`/`CaptionFontSize`
(moved onto `Appearance`, where they already live on disk).

`IAccountSettings` gets 2.4, plus `Session` and `Clear()`.

`AppSettings.Current` stays a static, like `SettingsService.Current` is today — it is genuinely
process-global, and 1745 sites already spell it that way. The swappable part is the store, set
once at startup, not the entry point.

## 5. Decisions for Fela

1. **Naming.** `IAppSettings`/`AppSettings.Current` and `IAccountSettings`? The codebase says
   "session" internally (`ISession`, `sessionId`) and "account" in the UI. `ISessionSettings` is
   more consistent with the code; `Session` is already overloaded against `Td.Api.Session`.

2. **Which of 2.4 should actually become global?** These are per-account today mostly by accident
   of `_container`. My reading:
   - genuinely per account: `UserId`, `UseTestDC`, `Chats`, `Notifications`, `AutoDownload`,
     `Video`, `UseSystemProxy`, `LastProxyId`, `IsSecretPreviewsEnabled`, `LastMessageTtl`
   - should be global (they are UI preferences, and the settings page presents them as app-wide):
     `IsReplaceEmojiEnabled`, `IsContactsSortedByEpoch`, `UseLeftTabsForForums`
   - unclear: `UseLessData` — a call setting, and `VoIP` is global; moving it there is tidier but
     changes behaviour for multi-account users.

   Anything moved needs a promote-on-first-run from the active account, or users lose it.

3. **`DistanceUnits` and `VolumeMuted`** (3.3) — global, I assume. Confirm.

4. **`AppearanceSettings` is two things.** Below the night-mode line it is a settings bag; above
   it, it owns a `Timer`, a `UISettings` subscription, `WindowContext.ForEachAsync` broadcasts and
   `LifetimeService.Current.ActiveItem.Resolve<…>()`. That half is a service, and it is what makes
   the settings layer depend on XAML. Split it or leave it? It is not required for the storage
   abstraction, but it is required before the settings layer can be called UI-free — and it
   touches the islands work (`notes/win32-xaml-islands.md`, 0.19).

5. **`PasscodeLock` is global but cleared per account.** `SessionImpl.Handle` calls
   `Settings.PasscodeLock.Clear()` on log-out, which wipes the passcode for *all* accounts. Is
   that intended?

## 6. Plan

Five steps, each independently shippable, each verifiable by running the app.

### Step 1 — Fix the bugs in place (no restructuring) — **done**

- `_video`, `_useSystemProxy` and `_lastProxyId` are instance fields now (3.1, 3.2)
- every app-wide property that read `_container` names `_local` instead: the six `AutoPlay*`,
  `IsPowerSavingEnabled`, `IsStreamingEnabled`, `IsDownloadFolderEnabled`, `VolumeLevel`,
  `VolumeMuted`, `DistanceUnits`, `ReportsCount`, `ReportsDate`, `AnonymousUserId`,
  `InstallBetaUpdates`, `EnabledProxyId`, `MigratedProxy`. A no-op on disk — `_container` was
  already the root everywhere these are read — and it makes them correct from either entry
  point. It also lets them keep their static caches honestly, and makes step 3 a pure move
- getters and setters name the same container (3.4); `UserId`'s intentional asymmetry is
  commented
- `Initialize` promotes `DistanceUnits` and `VolumeMuted` from the active account's container to
  the root when the root has no value (3.3)
- `Clear()` resets the account-scoped caches and deletes the `AutoDownload`, `Video` and
  `PinnedMessages` sub-containers, which `Values.Clear()` leaves behind (3.6). It no longer
  clears `_container`, which on `Current` would have wiped the entire root
- `AddOrUpdateValue` is `void` and writes straight through (3.7)

One file. Every change is a no-op for a single-account install, by 3.8 — which is also why none
of it needs a migration beyond the two promotes.

Left for step 3: `IsReplaceEmojiEnabled`, `IsContactsSortedByEpoch`, `UseLeftTabsForForums` and
`UseLessData` still read `_container`. They are agreed to become app-wide, but unlike the list
above they *are* written through a session, so moving them is a data migration, not a rename.

### Step 2 — Normalise the containers — **written, not yet verified**

The one step that touches user data, so it lands alone and before anything is restructured. It
ends 3.8: afterwards every key is in the container its final owner implies, `_container` means
"this account" on every session instance, and steps 3 and 4 become pure code moves.

The code half went in with it, because moving data without moving the pointer breaks the
setting: `IsReplaceEmojiEnabled`, `IsContactsSortedByEpoch`, `UseLeftTabsForForums` and
`UseLessData` now read `_local`, and are `static` like the app-wide settings around them — one
cache per process, or a write through one session leaves every other instance stale.
`DistanceUnits` and `VolumeMuted` folded into the same list, replacing the narrower
`PromoteToRoot` from step 1, so there is one mechanism rather than two.

After it, the only `_container` properties left are `IsSecretPreviewsEnabled`, `LastMessageTtl`,
the `Notifications` section, and the two dead version keys — i.e. `_container` finally means
exactly "this account".

**The keys involved.** 19 account-scoped keys sit in the root today, and none of them collide
with the 46 app-wide keys already there — checked by name across every settings class.

*Down, root → `{0}` (they stay per-account):*

| key | from |
|---|---|
| `InAppPreview`, `InAppVibrate`, `InAppFlash`, `InAppSounds` | `NotificationsSettings` |
| `ShowName`, `ShowText`, `ShowReply` | `NotificationsSettings` |
| `IncludeMutedChats`, `IncludeMutedChatsInFolderCounters`, `CountUnreadMessages` | `NotificationsSettings` |
| `IsSecretPreviewsEnabled`, `LastMessageTtl` | `SettingsService` |

*Stay at root, and get pulled up out of `{n}` for n > 0 (agreed app-wide in section 5.2):*
`IsReplaceEmojiEnabled`, `IsContactsSortedByEpoch`, `UseLeftTabsForForums`, `UseLessData`.

*Stay at root, unmoved:* `HasRemovedCollections`. It is on `NotificationsSettings` but it is a
one-shot app-level flag — `NotificationsService` reads it through `Current`, and it is the only
notification key that does. It becomes an `IAppSettings` member in step 3.

*Left alone:* `LongVersion` and `SystemVersion`. They are dead (2.5), so their root copies are
simply orphaned by the `_container` change; deleting stored values to tidy up is risk for no
gain, and step 5 removes the members anyway.

**The code change that has to accompany it.** Moving the data is only half:

```csharp
public SettingsService(int session)
    : base(session > 0 ? ...CreateContainer($"{session}", ...) : null)   // -> always the container
```

`_container` then equals `_own` on every session instance, and differs only on `Current`, where
it stays the root. Leave `_own` in place for now — collapsing the two names is step 3's job, and
keeping them distinct is what stops `Current.UseTestDC` from quietly reading the root instead of
throwing.

**Shape of the migration.** Move is copy-then-delete-source, which makes a re-run a no-op
without needing a version marker: a second pass finds no source key. A crash between the two
halves re-copies the same value and then deletes. A downgrade that writes the root again is
re-migrated on the next upgrade, which is the right answer, since the older build's value is the
newer one.

It runs from `Initialize()` — the first line of authored code in `App`, before anything can read
a setting. Sessions are enumerated from `LocalSettings.Containers` by integer-parsable name, so
it does not need `LifetimeService`, which is constructed later.

For the four keys going up, several accounts may hold a value and only one can survive. The rule
is the same one step 1 already used: **the active session's value wins** — which for session 0 is
the root copy, so it is uniform — then the `{n}` copies are deleted. Users with more than one
account lose the non-active accounts' setting for those four. That is inherent in making them
app-wide, not something the migration can avoid.

**Verification — outstanding.** The build is clean and the scoping was re-checked
mechanically: no static cache over a per-account container, no getter and setter naming
different containers bar `UserId`. The data path is *not* verified.

Offline inspection of a real profile turned out to be closed: `settings.dat` is held open by the
OS for as long as the package is registered, so it cannot be copied, and `reg load` on it needs
elevation. So the before/after diff can only be taken by deploying a build and launching it —
which runs the migration, once and irreversibly, against whatever profile it lands on. The
upward moves delete the `{n}` copies, by necessity: leaving them would let a stale account copy
overwrite the root on the next launch.

So the first run must not be against a profile that matters. Either a scratch package identity
with a deliberately constructed before-state, or `settings.dat` backed up first with the package
unregistered so the file is free. What to check afterwards, against a two-account profile with
every affected setting set to a non-default value:

- the twelve account-scoped keys are gone from the root and present in `{0}` with their values
- the six app-scoped keys are absent from every `{n}` and hold the active account's value at root
- `HasRemovedCollections` is still at the root
- a second launch changes nothing

### Step 3 — Introduce `ISettingsStore`, keep the object model

Purely internal; no key moves, no call-site churn.

- add `ISettingsStore` (4.1) and `ApplicationDataSettingsStore`
- rewrite `SettingsServiceBase` to hold an `ISettingsStore` instead of an
  `ApplicationDataContainer`; keep `GetValueOrDefault`/`AddOrUpdateValue` as the section-level
  API so the ~200 accessors do not change shape, and keep them public — `DiagnosticsViewModel`
  uses them with dynamic keys
- port the four leaks: `AutoDownloadSettings` (make it a section like the rest, or hand it a
  store), `LifetimeService`, `StickerDrawerViewModel`'s `Channels` container, and delete the
  three `ApplicationDataContainer` extensions from `Common/Extensions.cs`
- `LifetimeService` also wants `GetContainer(name, create: false)` — it currently *creates* a
  container for every numeric folder it scans, including ones it is about to delete

Exit criterion: `Windows.Storage` appears in exactly one file under `Services/Settings/`.

### Step 4 — Split the object model

The compiler does the work.

- add `IAppSettings`/`AppSettings` and `IAccountSettings`/`AccountSettings`; both build on
  `SettingsSection` over a store
- move each member per 4.3 and the answers to section 5; step 2 has already put every key in
  the right container, so this is a code move with no data behind it
- `SettingsService.Current.X` → `AppSettings.Current.X`: one mechanical identifier rename over
  1745 sites
- `Settings.X` where X moved to global → `AppSettings.Current.X`: ~150–200 sites, every one of
  them a compile error, so none can be missed
- do **not** add forwarding properties from `IAccountSettings` to `IAppSettings`. That is exactly
  the mess being removed, and it would hide the remaining call sites
- `ISession.Settings` and `ViewModelBase.Settings` change type to `IAccountSettings`

`SettingsService`/`ISettingsService` are deleted at the end of this step.

### Step 5 — Remove the dead weight

- delete `Version`, `SystemVersion`, `UpdateVersion`, `CleanUp`, `Container` (2.5)
- move `SettingsLegacyService` out of `Services` and rename it for what it is (navigation frame
  state, in memory) — or fold it into `FrameFacade`, its only caller

### Step 6 — Prove the seam (optional, only when something needs it)

Write a second `ISettingsStore` — a JSON file store with a debounced writer and an atomic
replace — and switch to it behind a diagnostics flag. Until an unpackaged host actually exists
this is a test of the abstraction rather than a feature; `ApplicationData.Current` keeps working
in a packaged desktop app, so the islands work does not need it.

## 7. Sizing

| step | files touched | risk |
|---|---|---|
| 1 | 1 | low — behaviour fixes, visible in the app |
| 2 | 1 | **the highest of the six** — it rewrites stored user settings |
| 3 | ~8 | low — internal, no key moves |
| 4 | ~250 | medium in volume, low in kind; every change compiler-forced |
| 5 | ~3 | none |
| 6 | ~3 new | isolated |

Step 4 is the only one that touches many files, and all of it is mechanical. Step 2 is the only
one that can lose data, and it is two files — the risk is entirely in the data, not the diff,
which is why it lands alone and gets a before/after dump rather than a review.
