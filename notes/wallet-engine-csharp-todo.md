# C# bindings for wallet-engine

Written 2026-08-20. Plan and resume point for consuming
[i582/wallet-engine](https://github.com/i582/wallet-engine) — the Rust TON wallet engine —
from Unigram.

Everything stated as fact below was read out of a clone of that repository, out of a clone of
`NordSecurity/uniffi-bindgen-cs`, or out of this repository. **Almost all of it has since been
built and run** — the engine on both Windows architectures, the generator and its 176-test
suite, the generated bindings on `netstandard2.0` and `net8.0`, and a UWP spike under .NET
Native. Where a claim is still inferred rather than measured, it says so.

## State, 2026-08-20

**Task 1 is done.** The generator question is settled: PR #176 works, was reviewed, and one
alignment patch sits on top of it. Nothing has been committed or pushed in any repository.

| Where | What |
|---|---|
| `C:\Source\uniffi-bindgen-cs` | branch `align-foreign-bytes-pin` off #176's head `0fc022a`. Two files modified, **uncommitted**. `stash@{0}` holds my superseded hand-written 0.32 port. |
| `C:\Source\Telegram\notes\wallet-engine-csharp-todo.md` | this file, untracked |

Verified numbers to date: generator builds on **Rust 1.88**; **176/176** tests pass on both
`net8.0` and `netstandard2.0`; generated output byte-identical to pre-#176 for all 35
existing fixtures, so the path all of wallet-engine's code takes is unchanged.

**Tasks 2 and 3's build questions are done too, and so is the .NET Native question.**
wallet-engine builds on Windows in 44s unpatched (ARM64 in 21s); the generator produces 13,911
lines of C# for the real API from a one-line config; it compiles clean on `netstandard2.0` and
`net8.0`; and **a UWP spike proves .NET Native both compiles and runs it**, callbacks from Rust
threads included (5.2).

**No technical unknowns are left on the binding route, and 4.5 is decided:** TON Org will not
support WalletKit, so the existing POC's WalletKit layer is retired and wallet-engine replaces
it. The wallet work is one commit on the local branch **`wallet`**, never merged — see 4.5 for
what survives the swap (most of the UI does).

**Next is ordinary app work:** Task 4's host implementations, on a rebased `wallet` branch. The
small open items are 3.4 (`panic = "abort"`), 3.5 (where the binaries live) and 1.8b (offering
findings upstream).

**Open decisions, both Fela's:** 1.8b (offer findings upstream — a public third-party repo, so
nothing is sent without a say-so) and 4.5 (whether wallet-engine retires
`Libraries/ton-walletkit-core` and the existing app-side wallet, which is a bigger call than a
checklist item and is not yet settled).

## State, 2026-09-12 — engine moved to master tip

The wallet landed on local `develop` (five commits, rebased, fast-forwarded), and the engine was
then updated from `6a13d61` to **`632fffa` (master tip, 2026-09-09), 58 commits on**. Fela's call
was master rather than the `v0.0.6` tag. Everything below was rebuilt and re-measured, not
carried over.

- **The uniffi pin did not move** — still `=0.32.0`, so Task 1's port and the fork stand
  unchanged.
- **Both DLLs rebuilt and vendored**: `x86_64` 46.7s → 2.08 MB, `aarch64` 29.0s → 1.97 MB
  (was 1.82 / 1.73 MB). Same toolchain 1.96.1, same `--locked`, no patches. 3.4 is still open:
  `panic = "abort"` is still in `[profile.release]`.
- **Bindings regenerated: 13,911 → 17,284 lines.** Both legs compile clean, 0 warnings:
  `net10.0`, and `netstandard2.0` with PolySharp — the leg `Telegram.csproj` needs.
- **Only one app-side break in the whole update.** `ProviderConfig` gained a middle parameter,
  `TonAddressString? DnsRootAddress`; `WalletService.AttachAsync` passes `null`, which leaves the
  engine on `default_dns_root_address(network)` rather than disabling resolution. Nothing else
  our code touches changed: both host interfaces are identical member for member, no type, record
  field, enum member or function was removed, and the 8 records that changed only gained fields.
- **New surface, all unused so far:** a second transport host (`WalletStatuslessHost` — the one
  that would carry the route-through-TDLib idea), key rotation, `sendBoc`, DNS resolution,
  encrypted comments, `ton://transfer` parsing, mnemonic-scheme detection, `ActivityStatus` and
  fee/comment fields on `ActivityItem`.

### Two traps this cost a detour on

- **`target/release/uniffi-bindgen-cs.exe` was stale.** The checked-in bindings had been produced
  by the *debug* build (2026-08-20 15:37), which is the only one that carried the 1.10 patches;
  the release exe predates them (09:52). Regenerating with it silently renamed every callback
  interface (`IWalletHttpHost` → `WalletHttpHost`) and would have broken both host classes.
  **Rebuild the generator before regenerating.**
- **A custom type cannot be reached by qualifying it** — `bindgen/templates/macros.cs` broke the
  variant/type name collision (`TonTransferPayload::Boc` carrying a `Boc`) by writing
  `WalletEngine.Boc`, and `Boc` is a `using` alias, not a namespace member: CS0234, on the one
  line of 17,284. The fork now carries a second patch for it — a `type_name_unaliased` filter
  that resolves a custom type to the type it aliases, and a third argument on
  `enum_parameter_type_name` that uses it before falling back to the namespace. Enum and error
  templates both. **Uncommitted, like the rest of the fork.**

### The service, rewritten around TDLib (2026-09-12)

TDLib gained the whole wallet surface in the schema of 2026-09-11 (`tonWalletState`,
`getTonWalletTransactions`, `sendTonWalletTransfer`, `sendTonCenterApiRequest`, the on-ramp family).
Fela's call: **TDLib creates the wallet, we bind and sign.** So `WalletService` no longer owns the
wallet's life — `tonWalletState` is the source of truth for identity and balance, arriving by
`updateTonWalletState`; history comes from `getTonWalletTransactions`, because only TDLib knows the
`peer_user_id` behind an address; and the engine signs but never broadcasts.

- `WalletLifecycle.CreateWallet` is gone from the service. `ImportAsync` became `BindAsync`, which
  derives from the phrase and **refuses a phrase whose address is not the account's** — compared on
  `ParseTonAddress(...).Raw`, since the two sides need not pick the same user-facing spelling.
- The engine's snapshot, pump and activity paging are deliberately unused: running them beside
  TDLib's would double every provider request and let the two disagree mid-refresh.
- `WalletHttpHost` is **deleted**, replaced by `WalletStatuslessHost` over `sendTonCenterApiRequest`
  (`WalletClient.NewStatusless`). The note in the old host - that `sendTonCenterApiRequest` had
  vanished from the layer, and that TDLib rewriting scheme and host would defeat the engine's
  redirect guard - is answered by the statusless trait, which claims no final URL at all.
- Send is the seam: `PrepareTransfer` returns `ExternalBoc` + `InternalBoc`, and
  `sendTonWalletTransfer` takes `regular_transfer_data` + `gasless_transfer_data`. **Unverified: that
  those bytes are the raw BOC.** `Boc` is standard padded base64 on the engine's wire, so the service
  base64-decodes it in one place (`Bytes`), which is the only line to change if the server wants
  something else. Same open question for the encrypted-comment body TDLib reports on a transaction.

## The shape

There is no hand-written binding code in wallet-engine to port. The Swift and Kotlin
bindings are **entirely UniFFI-generated** — `bindgen/apple/src/main.rs` is one line
(`uniffi::uniffi_bindgen_swift()`), `bindgen/kotlin` is the same, and `bindings/` is
gitignored. The public surface is declared with proc macros only, no UDL: 3
`#[uniffi::Object]`, 2 `#[uniffi::export(foreign)]` **async** traits, 54 records, 24 enums,
6 errors, 5 `custom_type!` newtypes over `String`, ~45 exported functions.

So "C# bindings" means getting a UniFFI C# backend to run against the crate, not writing a
binding layer.

`NordSecurity/uniffi-bindgen-cs` is feature-complete for this surface — async in both
directions, foreign async traits, custom types, and `netstandard2.0` as a first-class
target (its own test project builds `netstandard2.0;net8.0` with PolySharp, which is
exactly what `Telegram.csproj` already carries). The gap is one version: wallet-engine pins
`uniffi = "=0.32.0"`, the generator is at `+v0.31.0`, and a metadata-contract mismatch
means it will not even load the library.

The size of that port is not a guess. `bindgen/cpp/` in wallet-engine is a vendored fork of
`NordSecurity/uniffi-bindgen-cpp` `v0.9.0+v0.29.4` that TON Core forward-ported to 0.32
**and** added async to. Diffed against the upstream tag: **21 files, ~358 changed lines**.
The C# job is one version instead of three, with async already present.

## Decisions taken (Fela, 2026-08-20)

- **Tier-1 Rust targets, not the UWP ones.** No `*-uwp-windows-msvc`, no `-Z build-std`, no
  `no_std`. The app has `fullTrust` and ships a normal desktop exe, so the import-table and
  WACK problem that forced tlottie to `no_std` does not apply here. wallet-engine could not
  have gone `no_std` anyway — `serde_json`, `url`, `ed25519-dalek`, `std::sync`, async and
  `getrandom` all need it.
- **Records are a non-issue.** The generator emits C# `record` for all 54 records and the
  non-flat enums; Unigram already ships records on .NET Native.
- The `#if NET6_0_OR_GREATER` cancellation gap and the `Marshal` reverse-P/Invoke question
  are **parked, not closed** — Task 5.

## Task 1 — Forward-port uniffi-bindgen-cs to UniFFI 0.32

**Mostly already done upstream.**
[PR #176 "Upgrade to uniffi-rs 0.32.0"](https://github.com/NordSecurity/uniffi-bindgen-cs/pull/176)
by Dennis Ameling — 22 files, +723/−235 — does this and more. Opened 2026-07-10, last
touched 2026-07-24, still **open and unreviewed** as of 2026-08-20: no review, no CI beyond
the DCO check, and one maintainer comment asking someone to review it. So it is usable but
not blessed, which makes 1.8 the real decision.

- [x] **1.1** **A fork after all — but only because of 1.10.** For a while the answer was
  "no fork, just a pinned revision": #176 needed no changes for wallet-engine's surface, so
  there was nothing to carry. Aligning the borrowed-bytes null handling (1.10) changed that
  — we now carry one patch, which is what a fork is for. The clone sits at
  `C:\Source\uniffi-bindgen-cs`, matching how tlottie is kept; `origin` is
  NordSecurity, branch `align-foreign-bytes-pin` off PR #176's head
  `0fc022aa1d73fb1dda91a778b63f2824d7dca58b`. **Nothing is pushed anywhere.**
  - The PR is **cross-repo** — its head is `dennisameling/uniffi-bindgen-cs`, and the branch
    does not exist in NordSecurity's repository. A `cargo install --git` against the
    author's fork would break if they delete it. Two things cover that: GitHub keeps
    `refs/pull/176/head` on NordSecurity regardless (`git fetch origin refs/pull/176/head`),
    and the local clone already contains the commit.
  - Releases carry **no binaries** — they are source tags, `assets: []`. So the generator is
    built from source whichever revision we pin, which flattens most of the difference
    between taking the PR now and waiting for a tag.
  - The generator is **not in the app's build graph.** It emits one `.cs` file; check that in
    and nothing in Unigram's build needs the tool. It is re-run only when wallet-engine's API
    moves, and UniFFI's startup checksum check makes drift loud rather than silent.
- [x] **1.2** The whole 0.31 → 0.32 break surface is **two new `Type` variants**. Bumping
  only the version numbers and running `cargo check` produces exactly two errors, both
  non-exhaustive matches: `Type::Set` and `Type::Box`. `Set` is a new collection with the
  same i32-count-plus-items wire format as `Sequence`, so it maps onto C# `HashSet<T>`;
  `Box` is documented upstream as scaffolding-only and bindings just use the inner type. I
  reached the same two answers by hand before finding the PR, down to using
  `new HashSet<T>()` rather than the capacity constructor — `HashSet<T>(int)` does not
  exist on `netstandard2.0`.
- [x] **1.3** ~~`CrateConfigSupplier::from_cargo_metadata_command` changed signature.~~
  **The 0.32 changelog is wrong about this.** The published crate still takes a plain
  `bool`, and no generator change is needed. Confirmed twice over: the cargo check above
  produced no such error, and the PR author reports the same.
- [x] **1.4** ~~`BindgenPathsLayer::get_config()` replaced by `GlobalConfig`.~~ Same story —
  `BindingGenerator`, `generate_external_bindings` and `library_mode::generate_bindings` are
  all unchanged.
- [x] **1.5** The `ForeignBytes` change is indeed a no-op **for wallet-engine** — every
  `&[u8]` in it is `pub(crate)` or private, so nothing on the exported surface is affected.
  It is not a no-op for the generator: before #176 a `[ByRef] bytes` argument produced C#
  that did not compile (`cannot convert from 'RustBuffer' to 'ForeignBytes'`), and the PR
  adds `GCHandle`-pinned zero-copy lowering scoped to the call. This is the main reason to
  take the PR rather than hand-roll the two `Type` arms.
- [x] **1.6** **Do not bump the Rust toolchain.** I had moved the pin to 1.97.1 before
  finding the PR; that was wrong. #176 deliberately keeps `1.88` by pinning `cargo-platform`
  to `0.3.2` — which is what uniffi-rs does in its own lockfile — because the `test-bindings`
  CI job runs in a prebuilt container with 1.88 baked in, and bumping would force
  republishing that image. wallet-engine's own `1.96.1` pin applies to *its* build, not to
  the generator's.
- [x] **1.7** Suite run and verified on this machine, on the PR branch unmodified.
  `cargo build --release -p uniffi-bindgen-cs` succeeds **on Rust 1.88** (1m48s), confirming
  no toolchain bump is needed. `build.sh` takes **68 minutes** — it compiles every fixture
  crate, so budget for it. `generate_bindings.sh` emits 36 files (the 35 existing plus the
  PR's new `uniffi_032_types`). Result: **175 tests, 175 passed.**
  - **`test_bindings.sh` does not work with the .NET 10 SDK.** It runs `dotnet test`, and
    .NET 10 dropped VSTest support for the Microsoft.Testing.Platform runner this suite
    uses: *"Testing with VSTest target is no longer supported"*. Nothing runs, and the
    script still reports success. Run the test project directly instead, which is the
    MTP-native path and needs no repository change:
    `dotnet run --project dotnet-tests/UniffiCS.BindingTests/UniffiCS.BindingTests.csproj`
  - **Two tests fail on this machine for locale reasons, not PR reasons.**
    `TestRondpoint.TestStringifier` and `TestNumericLimits.NumericLimitsAreTheSame` both
    format floats through the current culture, so on a comma-decimal locale C# emits
    `1,401298E-45` and Rust's `parse::<f32>()` rejects it. With
    `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` it is 175/175. This is a pre-existing bug in
    the suite, worth reporting upstream separately from #176.
- [x] **1.7b** **The `netstandard2.0` leg now runs, and passes.** `UniffiCS.csproj`
  multi-targets `netstandard2.0;net8.0` so both legs always *compile*, but
  `UniffiCS.BindingTests` targets `net9.0` and resolves the **net8.0** asset — so by default
  every test exercises the leg we would not ship. Pinning the reference:

  ```xml
  <ProjectReference Include="..\UniffiCS\UniffiCS.csproj"
                    SetTargetFramework="TargetFramework=netstandard2.0"/>
  ```

  gives **175/175** against the netstandard2.0 assembly, confirmed by hashing the copy in
  the test host's output directory against both builds. That covers the `#else` side of all
  11 conditional sites in the generated code: `DllImport` instead of source-generated
  `LibraryImport` (2 sites), heap `byte[]` instead of `stackalloc Span` in `BigEndianStream`
  (7 sites — same loop bodies, so this is an allocation difference rather than a second
  implementation), `object` instead of `System.Lock` (1), and the missing `Task.WaitAsync`
  (1, see 5.1). The change is one line and was reverted; the fork is pristine at #176's
  commit.
  - **Still not covered: .NET Native.** This proves the netstandard2.0 *IL* is correct on
    the .NET 9 runtime. It says nothing about the .NET Native toolchain compiling and
    running it, which is 5.2's question.
- [x] **1.8** **Reviewed. Verdict: sound, safe to build on.** The commit is AI-assisted
  (`Co-Authored-By: Claude Opus 4.8`), so its headline claims were checked rather than
  trusted, and they hold.

  Verified independently:
  - **Output is byte-identical for all 35 pre-existing fixtures.** Reverted only
    `bindgen/templates/macros.cs` to `main`'s version on the PR branch, rebuilt, regenerated,
    diffed: zero differences outside the new fixture. The experiment was sensitive — the new
    fixture *did* differ, showing the old `FfiConverterByteArray.INSTANCE.Lower(@v)` where
    `ForeignBytes` is now required. This matters to us more than any other check: wallet-engine
    has no borrowed-bytes arguments, so **all** of our generated code takes the unchanged path.
  - `is_borrowed_bytes()` (`self.by_ref && matches!(self.type_, Type::Bytes)` — precisely
    `&[u8]`, not all bytes) and `apply_exclusions()` are real upstream `uniffi_bindgen` 0.32
    APIs, not invented locally.
  - `_UniffiHelpers` carries both generic and void `RustCall`/`RustCallWithError` overloads,
    so the new statement-bodied lambda resolves correctly for void and non-void calls; the
    generated void case renders without a `return`, as it must.
  - The `Set` and `Box` handling matches what Kotlin and Swift do upstream, and matches what
    I had written by hand before finding the PR.

  Findings, none blocking:
  - **The Rust 1.88 floor is held by a transitive `Cargo.lock` entry that nothing names.**
    `cargo-platform` appears in no `Cargo.toml`; 0.3.2 declares `rust_version = 1.88` and
    0.3.3 declares `1.91`. Any `cargo update` or dependency bot silently breaks the "Requires
    Rust 1.88+" promise in `README.md` and `AGENTS.md` with an MSRV error whose cause lives
    only in the PR's commit message. An explicit `cargo-platform = "=0.3.2"` with a one-line
    reason, or a note in `VERSION_UPGRADE.md`, would fix it. This is the one I would want
    changed before merge.
  - **`lower_arg_list` emits `_fb_x.Bytes` for borrowed-bytes arguments regardless of caller,
    but only `ffi_call_body` declares `_fb_x`.** Three async call sites — macros.cs:92, :95,
    :126 — call it without the pin. Unreachable today, because rustc rejects `&[u8]` in async
    functions and in foreign traits, and the failure would be a compile error rather than
    corruption; but the invariant rests on an unstated upstream constraint and would surface
    as a bare `CS0103`.
  - `ForeignBytesPin` is a **mutable struct implementing `IDisposable`**. Correct as generated
    (`using var`, never copied), but a copy would double-`Free` the `GCHandle`. A class or a
    `readonly struct` removes the footgun.
  - **Null handling diverges from the rest of the generator.** `ForeignBytesPin` throws
    `ArgumentNullException`, where every other array path silently treats null as empty
    (`details/1-empty-list-as-default-method-parameter.md`; both `SequenceTemplate` and the new
    `SetTemplate` write 0 for null). Throwing is arguably right for a borrowed pointer, but the
    difference is undocumented and no test asserts it.
  - The *zero-copy* property itself is untested and essentially untestable from managed code;
    what the fixtures prove is that the right bytes arrive. Fine, but the claim rests on
    inspection.

  Only ever needs to become a real fork if we decide to fix 5.1, which is a template change.
- [ ] **1.8b** Nothing has been sent to the PR. Offering the netstandard2.0 test gap from 1.7b
  and the `cargo-platform` finding would help it land, but posting to a third-party repository
  is Fela's call.
- [ ] **1.9** #176 also adds an `exclude` config option, which the author flags as going
  beyond a strict version bump and offers to split out. Irrelevant to us either way, but
  it is the part most likely to attract review churn and delay the merge.
- [x] **1.10** **Borrowed-bytes handling aligned with the generator's existing conventions.**
  Branch `align-foreign-bytes-pin`, two files, +24/−5, **uncommitted** in the working tree.
  - `ForeignBytesPin` no longer throws `ArgumentNullException`. A null array now means an
    empty one, as it does for every other collection argument — and this is not merely
    stylistic: `details/1-empty-list-as-default-method-parameter.md` records that `null` is
    how an empty collection is *spelled* at a call site, because C# cannot express a non-null
    collection default. Throwing would have broken that idiom for any `&[u8]` argument with a
    default value. `SetTemplate` in the same PR already follows the convention, so
    `ForeignBytesPin` was the sole outlier, inconsistent even within #176.
  - Null and empty now take the same branch and pin nothing. Safe by design, not by luck:
    `ForeignBytes::as_slice` in `uniffi_core` reads a null pointer with zero length as `&[]`
    and panics only on a null pointer with *non-zero* length.
  - `internal struct` → `internal sealed class`, matching `UniffiForeignFutureHandle`, the
    generator's only other disposable helper. This also removes the double-`Free` a struct
    copy could cause. It costs one small allocation per pinned argument per call — irrelevant
    to us, since wallet-engine has no borrowed-bytes arguments at all.
  - Added `ByrefNullBytesAreTreatedAsEmpty`, and corrected the existing
    `ByrefEmptyBytesArePassedByPointer` comment, which claimed a zero-length array "still
    pins to a valid, non-null pointer" — no longer true after this change.
  - Verified: **176/176 pass on net8.0 and 176/176 on netstandard2.0** (hash-checked). The
    union of every changed line across all 36 regenerated files is exactly the
    `ForeignBytesPin` block — nothing else moved.
  - Deliberately **not** done, all from the 1.8 review and all better raised upstream than
    carried as local patches: the undocumented `cargo-platform = 0.3.2` pin, and a guard for
    `lower_arg_list` emitting `_fb_x.Bytes` on the async path.

## Task 2 — Generate against wallet-engine — **done**

Clone at `C:\Source\wallet-engine`, `6a13d61`, unmodified.

- [x] **2.0** **wallet-engine builds on Windows out of the box.** `cargo build --locked` on
  `x86_64-pc-windows-msvc`, **43.77s**, no patches, no feature juggling — which also settles
  **3.3**. rustup pulled the pinned 1.96.1 itself. This is the first Windows build of the
  crate and it was uneventful.
- [x] **2.2** `[bindings.csharp]` needs **two lines**:

  ```toml
  [bindings.csharp]
  cdylib_name = "wallet_engine"
  namespace = "WalletEngine"
  ```

  The namespace default is `uniffi.wallet_engine`; Fela's call is plain **`WalletEngine`**.
  `lib.rs` sets it with `get_or_insert_with`, so a configured value wins. Two things do *not*
  follow it, both harmless: the static class of free functions stays `WalletEngineMethods` (it
  is built from the crate namespace, and already reads well), and the generated **file** stays
  `wallet_engine.cs` for the same reason — which is convenient, because renaming the namespace
  needs no `.csproj` edit. Re-verified end to end after the rename: AOT build clean, all three
  spike probes pass. The
  five `custom_type!` newtypes need no configuration — unconfigured they emit
  `using TonAddressString = String;` aliases (and alias their converters to
  `FfiConverterString`), which is what Kotlin and Swift do by default too. Validation still
  happens, on the Rust side during lift, surfacing as an error rather than a C# type error.
  Giving them real C# wrapper types via `type_name`/`into_custom`/`from_custom` is possible
  but is a Task 4 design call, not a prerequisite. Defaults for the rest are sane: namespace
  `uniffi.wallet_engine`, free functions on a `WalletEngineMethods` static class, `internal`
  access — right if the file is compiled straight into `Telegram.dll`.
- [x] **2.4** Generated and read. **13,911 lines / 528 KB**, one file, first try:

  | | |
  |---|---|
  | records / enums / exception classes | 60 / 18 / 14 |
  | objects | `WalletClient`, `WalletLifecycle`, `TonConnectSession`, each `IDisposable` |
  | host interfaces | `WalletHttpHost`, `WalletPlatformHost` |
  | `Task`-returning members | 32 |
  | 221 Rust type names | **no collisions** with C# keywords or common BCL names — no `rename` config needed |

  The async is genuinely `Task`-based, not the blocking condvar the C++ backend in that repo
  uses: `Task<HttpResponse> ExecuteHttp(HttpRequest @request)`,
  `Task<byte[]> ReadProtectedSecret(ProtectedSecretRead @request)`. Rust doc comments carry
  through as XML docs, including `<exception cref="HttpHostException">` tags.
- [x] **2.5** **It compiles, on both legs.** A scratch project mirroring `UniffiCS.csproj`
  (`netstandard2.0;net8.0`, PolySharp, `AllowUnsafeBlocks`, `Nullable`) builds the generated
  file with **0 warnings, 0 errors** — netstandard2.0 368 KB, net8.0 372 KB. The
  netstandard2.0 leg is the one `Telegram.csproj` needs.
  - Note for later: `dotnet build` crashed with `MSB4166: Child node exited prematurely`
    until run with `-m:1`. Single-node works; a parallel-node crash, not a compile error.
- [x] **2.1 / 2.3** ~~`bindgen/csharp` crate, `xtask/src/bindings/csharp.rs`, justfile
  targets, `csharp.yml`.~~ **Not needed, and not ours to write.** Those are changes to
  wallet-engine's own repository, for wallet-engine's own release pipeline. As a consumer we
  invoke the generator directly against the built cdylib —
  `uniffi-bindgen-cs --library <dll> --config <toml> --out-dir <dir>` — which is exactly what
  produced the output above. They only become relevant if TON Core want a C# binding in their
  release matrix, which is a conversation, not a task.
  - Minor naming wart, pre-existing upstream behaviour rather than anything #176 introduced:
    object interfaces get an `I` prefix (`IWalletClient`) but callback interfaces keep the
    Rust name (`WalletHttpHost`), because the implementation class takes the `Impl` suffix.

## Task 3 — Build the native library for Windows

- [ ] **3.1** wallet-engine ships **no Windows binary at all** today — `dist/native.rs`
  lists Linux and macOS only, and the release matrix is those plus an Android AAR, a Swift
  package and a wasm/TypeScript package. Windows is a new platform for them regardless of
  language, which is worth saying to whoever proposed the library.
- [x] **3.2** **Both architectures build.** `x86_64-pc-windows-msvc` 43.77s → 8.1 MB
  `wallet_engine.dll`; `aarch64-pc-windows-msvc` 20.60s → 7.2 MB. No `-Z build-std` and no
  nightly: rustup ships a prebuilt `rust-std` for the ARM64 target, which is the fact that
  actually matters rather than the tier number.
  - Trap worth remembering: the ARM64 target has to be added **for the toolchain
    wallet-engine pins**, not for whatever is default. `rustup target list --installed` showed
    aarch64 present, but that was for `stable`/1.97.1; the 1.96.1 toolchain rustup
    auto-installed from `rust-toolchain.toml` came with the host target only, and the build
    failed with `can't find crate for core` across two dozen unrelated crates. Fix is
    `rustup target add aarch64-pc-windows-msvc --toolchain 1.96.1`.
- [x] **3.3** Dependency set builds clean on windows-msvc — `getrandom 0.3.4`,
  `ed25519-dalek`, `num-bigint`, `url` and the vendored `ton` crate all compiled without
  intervention on both architectures. Settled by 2.0.
- [ ] **3.4** Decide about `panic = "abort"` in `[profile.release]`. UniFFI turns a Rust
  panic into an `UnexpectedError` through `catch_unwind`, and `panic = "abort"` defeats
  that — any engine bug becomes a process abort with a crash dump instead of a catchable
  exception. Reasonable for a phone, less obviously right for us. A local profile override
  is a one-line change if we want unwinding.
- [ ] **3.5** Decide whether we consume upstream release artifacts or build the dll
  ourselves, and where the binaries land — the `Libraries/` convention, alongside
  `tdjson/x64` and `rlottie/{x64,ARM64}`.

## Task 4 — Consume it in Unigram

### The design (agreed 2026-08-20)

**Build on the engine's store, do not mirror it.** `WalletClient` is already a revisioned
immutable store: `Snapshot()` returns a complete `WalletSnapshot` that never changes — account,
activity, NFTs, send state, each with its own `ResourceState` — and `WaitForChange(revision)`
resolves once a newer one exists. Every command returns a `WalletUpdate` and advances it. That
is a redux store, and the POC's central mistake was keeping a second mutable copy of the same
state beside it. **The new service owns no state; it owns a pump.**

**One wallet per Telegram account** (Fela's constraint), which removes every collection,
selection and "active wallet" concept from the design.

Three layers:

1. **Hosts — infrastructure only, no orchestration.**
   - `TonHttpHost : WalletHttpHost` — honours `timeout_ms`, rejects redirects, returns the
     observed `final_url`, and keeps a cancellation registry keyed by `HttpRequestId` that
     remembers a cancellation arriving *before* its request registers (the engine's docs
     require this explicitly).
   - `TonPlatformHost : WalletPlatformHost` — a thin adapter that delegates to two app-level
     interfaces rather than doing both jobs itself: **`IProtectedSecretStore`** (DPAPI plus the
     `RequireUserPresence` policy) and **`IWalletJournal`** (the versioned compare-and-swap).
     The journal is the subtle half and is worth being testable on its own, so it is defined in
     app primitives (`recordId`, `slot`, `version`, `payload`) rather than in generated types.
2. **`WalletService`** — one per Telegram account, the only class that knows the engine exists.
   Owns the `WalletLifecycle`, at most one `WalletClient`, the hosts, and the pump. Exposes an
   app-shaped `WalletState`, a `StateChanged` event, and thin async commands. (I first called it
   `WalletSession`, borrowing the engine's vocabulary; Fela's point stands that everything
   session-scoped in this app is `*Service` and the container already *is* the session.)
3. **View models bind to `WalletState`**, never to a generated record.

The pump is the entire threading story:

```csharp
while (!_stopped) {
    var snapshot = await _client.WaitForChange(_revision);
    _revision = snapshot.Revision;
    Publish(Project(snapshot));
}
```

**Correction to the original sketch: the service does not marshal to the UI thread.** It cannot —
a session outlives any one window and Unigram can have several, so there is no single dispatcher
for it to marshal to. `StateChanged` is raised from the pump's thread and the *view model* hops,
via `Dispatcher.Dispatch` (the fire-and-forget overload; discarding a `DispatchAsync` task would
have been the wrong tool). Subscription is a named method with a matching `-=` in
`OnNavigatedFrom`.

Decisions taken with it:

- **Project, don't expose.** The snapshot is mapped into app-owned models. The deciding
  constraint is not taste: the wallet XAML uses `{Binding}` in 8 places, and anything reached by
  `{Binding}` needs `[GeneratedBindableCustomProperty]` under AOT — an attribute we cannot add
  to generated code. Projecting also keeps `access_modifier` at `internal` so the generated
  surface cannot leak into public signatures by accident.
- **`WalletSession` is registered in the session container** via `[GenerateResolver]`, not hung
  off `ClientService`. The wallet does not belong to TDLib.
- **`record_id` stays a fresh id per wallet creation** — *not* the Telegram account id, however
  tempting one-wallet-per-account makes that. `delete_wallet`'s own documentation says it "does
  not delete application metadata or journal records", and `SEND_SLOT` holds durable pending
  sends that `ResolvePending()` reads at startup. Reusing an id would hand a new wallet the old
  wallet's pending send, against an address that no longer exists. A fresh id makes stale rows
  unreachable by construction; cleanup on delete is then hygiene rather than correctness.
- **The TON network follows the Telegram one.** `IClientService.Options.TestMode` decides mainnet
  vs testnet (Fela's call), so there is no separate switch and a test-DC client cannot move real
  coins. The descriptor records the network it was created with, so a restored wallet keeps its
  own regardless of the option's current value.
- **Two things the engine does not provide, both already available in the app.**
  - *The BIP-39 word list.* Vendored verbatim from the engine's own
    `vendor/ton/resources/mnemonics/wordlist_en.txt` into `Assets/Ton/`, so what the import screen
    accepts and what the engine accepts cannot drift. 2048 words, `abandon`…`zoo`.
  - *USD rates.* `IClientService.Options.MillionGramToUsdRate`, already wired into
    `OptionsService` (Fela's steer — I had been about to treat this as an open problem). It is
    whole dollars per 1,000,000 grams, so nanograms → cents is `balance * rate / 1e13`, done in
    `BigInteger` and only converted to `double` for formatting.
- **HTTP goes out directly, not through TDLib.** The POC used `sendTonCenterApiRequest`, which
  exists only in `td_api.old.tl` (2026-07-31) and is absent from the 08-07 and current 08-19
  layers — so `TonWalletHost.cs` does not compile today. Nothing replaced it; the current layer
  has only `getTonTransactions`, which is Telegram's internal ledger. This is the better outcome
  anyway: a proxy that rewrites the base URL would have defeated the engine's `final_url`
  redirect guard, which now works as designed.

### Progress

- [x] **Branch.** `wallet` rebased onto develop — was 1 ahead / 370 behind, now 1 ahead / 0
  behind. Three conflicts, all from `2e7cd41ae "Generate the session container instead of
  pasting it"`: `Session.g.cs` and `TypeContainerGenerator.cs` are deleted in develop (the DI
  container is now source-generated from a `[GenerateResolver]` attribute, so the POC's
  hand-registration of the wallet view models is obsolete), plus a two-hunk `Telegram.csproj`
  conflict. Worth remembering: `git rebase --continue` reported *"You must edit all merge
  conflicts"* while `git status` said all conflicts were fixed — the real cause was an unrelated
  dirty generated file (`Resources.cs`, a regeneration timestamp), which `git update-index
  --refresh` names and nothing else does.
- [x] **Layer 1 — the hosts.** Five files under `Telegram/Services/Ton/`, all wired into
  `Telegram.csproj` (which is not globbed, so every one needed an explicit entry):
  - `IProtectedSecretStore.cs` and `IWalletJournal.cs` — app-level contracts with their own
    failure enums mirroring the engine's classifications one for one, so the stores never
    reference a generated type and the adapter maps without interpreting.
  - `TonPlatformHost.cs` — translation only, no state, no decisions.
  - `TonHttpHost.cs` — direct HTTPS, redirects refused (`AllowAutoRedirect = false`), an 8 MB
    response cap enforced while reading rather than trusted from `Content-Length`, timeout
    distinguished from cancellation by which token fired, and transport failures classified into
    the engine's kinds. Cancellation uses one `GetOrAdd` keyed by request id, which handles a
    cancel arriving *before* its request for free; unclaimed entries are bounded to a 64-deep
    window so a cancel for a request that never arrives cannot accumulate.
  - `Generated/wallet_engine.cs` — the bindings, alongside the `uniffi.toml` that produced them.
  - Diagnostics deliberately carry only exception type names: provider URLs can hold an API key
    and the engine records what it is given.
  - **Verified**: compiled standalone against `netstandard2.0` and `net8.0` with the app's own
    settings (`Nullable` disabled, `LangVersion 14`) — 0 warnings, 0 errors. The files depend
    only on the bindings and the BCL, so this needs no app build.
- [x] **WalletKit removed** — `WalletService.cs`, `TonWalletHost.cs`, `TonProtectedStore.cs`, the
  `Ton.WalletKit` project reference, the `twk.dll` copy, and `ClientService.Wallet`. Native
  libraries vendored at `Libraries/wallet-engine/{x64,ARM64}/wallet_engine.dll` (1.82 / 1.73 MB
  release), replacing the `twk.dll` entry.
- [x] **Layer 2, and the app builds.** `Telegram.Modern.csproj` compiles with **0 errors and no
  warnings from any wallet file**, producing a 16.4 MB `Telegram.dll`.
  - `WalletState` — immutable projection, replaced wholesale, no generated type on it.
  - `IWalletService` / `WalletService` — registered in `Session.Registrations.cs` under `Lazy`,
    resolved into view models by constructor and elsewhere via `Session.Resolve<T>()`. Takes
    `IClientService`, which supplies both the session id and the two options below.
  - `WalletSecretStore` — DPAPI, one file per entry, presence byte *outside* the protected blob
    so a read that needs verification can check the policy before unprotecting.
  - `WalletJournalStore` — one file, compare-and-replace inside one lock, written aside and moved
    into place, and the in-memory entry rolled back if the write fails so it can never claim a
    durability it did not achieve.
  - **Balance is a plain `BigInteger`** (Fela's call — no wrapper struct). The unit lives in the
    parameter names: `Formatter.TonBalance(BigInteger nanograms, …)`.
  - UI repointed: both view models, `WalletPage`, `WalletSharePopup` (which only ever wanted the
    address, so it now takes a `string`), `WalletImportPopup`, `WalletTestPopup`, `BlankPage`.
    `Ton.WalletKit` is gone from the tree.
- [x] **A generator bug the app found, worth sending upstream.** `Telegram/CsWinRT.cs` has
  `global using Object = Telegram.Td.Api.Object;`, so bare `Object` means TDLib's type
  everywhere in this assembly — and `FFIObjectUtil` in the generated helpers declares
  `DisposeAll(params Object?[])` and `Dispose(Object?)`. Three compile errors, none of them our
  code's fault. Fixed in the fork by emitting the **`object` keyword**, which no `global using`
  alias can rebind; `TypeCode.Object` beside it is an enum member and stays. This is a real
  portability bug in `uniffi-bindgen-cs` — any codebase with an `Object` alias in scope hits it —
  and a good second contribution alongside 1.8b.
- [x] **First runtime bug: `SendAlreadyInProgress` from `ResolvePending` at attach.** Root cause,
  from `refresh.rs:29`: **`refresh` runs its own best-effort `resolve_pending` internally** —
  *"a client has no runtime of its own, so startup recovery is driven by"* the host calling
  refresh. My explicit `ResolvePending()` at attach raced that one, and the loser hit the
  engine's `active_send || active_resolution` guard. The README says the same thing twice
  ("Best-effort resolvePending, failure does not block observation"), which is worth reading
  before adding a call the engine already makes.
  - Fix: attach now fires a best-effort **`Refresh`**, which recovers *and* loads the first real
    snapshot. Gating on `SendSnapshot.Phase` instead would have been wrong — a fresh client always
    reports `Idle`, because reading the journal is precisely what `resolve_pending` does.
  - Three adjacent defects the investigation exposed, all fixed: `AdoptAsync` attached over an
    existing client without detaching it (a leaked client and pump); `AttachAsync` had no
    `_client != null` guard of its own; and the task was fire-and-forget, which is why this
    surfaced as an unobserved exception rather than an error.
  - `Publish` also raised its event while holding `_mutex`. Split into `SetState` (under the lock)
    and `Raise` (after releasing it), matching the discipline the engine keeps with its own lock.
- [x] **`StateChanged` replaced by `UpdateWalletState` through `IEventAggregator`** (Fela's call —
  it is how every other change in the app travels). `ViewModelBase.Subscribe`/`Unsubscribe` then
  handles teardown, so the manual `+=`/`-=` pair is gone, and handlers hop with `BeginOnUIThread`
  as the app's others do. The update carries the state, so a subscriber cannot observe a newer one
  than the update it is handling.
- [x] **`Telegram.Services.Ton` → `Telegram.Services.Wallet`** (Fela's call). `TonHttpHost` and
  `TonPlatformHost` keep their prefix deliberately: `WalletHttpHost` and `WalletPlatformHost` are
  the generated interfaces they implement.
- [x] **The engine derives an experimental contract, not standard W5.** Found while diagnosing a
  wallet that imported with the right key but a zero balance. `WalletVersion::Wallet` in the
  vendored `ton` crate is **not** `V5R1` — the code table says so outright: *"Built from
  https://github.com/tolk-vm/wallet-v5-experimental at b420256f… with Acton 1.1.0"*, loaded from
  `wallet_v5_experimental.code`. wallet-engine pins its hash on purpose
  (`wallet_code_matches_upstream_hash`), so this is a deliberate choice by TON Core, not an
  oversight.
  - Proved rather than guessed: a probe (`scratchpad/probe`, ~60 lines against the vendored crate)
    derives every version/wallet-id combination from a public key. Fela's funded address matches
    **`V5R1` + mainnet wallet id** exactly; the engine's `Wallet` variant gives a different
    address. The key derivation is correct — both descriptors carry the same public key.
  - **This is intentional and transitional** (Fela, after checking): TON plans a new version of
    the contract, and the experimental one is where that is heading. Not a defect to report, and
    not worth re-raising.
  - It does still mean that, until that version lands, a wallet created in Unigram is invisible to
    every other TON wallet — same phrase, different contract, different address — and an existing
    wallet a user imports shows empty. Neither is fixable app-side; the engine exposes no version
    selection. Fine while this is developer-only, and the thing to re-check before it is not.
  - Diagnostic worth keeping: `getAddressInformation` on both networks settles "where is the
    balance" in one call, and `uninitialized` does **not** mean empty — a funded wallet that has
    never sent anything is uninitialized and still holds its balance. The engine parses the
    balance independently of status, so that path was never the problem.
- [x] **Network follows `Options.TestMode` alone.** I had added a `Diagnostics.WalletTestNetwork`
  override to work around the balance problem; it was removed once the cause turned out to be the
  contract. Fela's objection stands on its own though: which chain a wallet is created on decides
  where its funds can ever be reached from, and a debug switch reachable from the diagnostics page
  must not be able to change that. The expression was also formatted so that `||` looked like it
  bound *inside* the ternary rather than outside it — correct code that reads as wrong.
- [x] **Wallet data lives beside TDLib's, and says which network it belongs to.**
  `LocalState\{sessionId}\wallet\` rather than `LocalState\wallet\{sessionId}\`, since the two
  belong to the same account and should be found, backed up and deleted together. Files carry a
  `_test` suffix under `Options.TestMode` — `journal_test.bin`, `descriptor_test.bin`,
  `{hash}_test.secret` — because the session id is the same for both and a wallet must never read
  the wrong network's key. TDLib does not suffix its own database; a wallet has more to lose.
  - **The stores are built lazily, not in the constructor**, because `Options.TestMode` is not
    known until TDLib has reported its options and this service is resolved before that. Freezing
    the suffix in the constructor would point a production session at the test files, or the
    reverse.
  - Data written under the old layout (`LocalState\wallet\{0,2}\`) is orphaned by this and can be
    deleted; it is test-wallet data only.
- [x] **Create is reachable from the UI.** `CreateAsync` existed but nothing called it, so with
  no wallet the app could only offer import. **Ctrl**-click the wallet button on `BlankPage`
  creates one, beside the existing **Shift** for delete. A developer affordance, not the
  eventual flow: it does not present the recovery phrase, which the engine says to show once
  at creation. Nothing is lost by that — `RevealRecoveryPhraseAsync` can produce it — but the
  real create flow has to show and confirm it.
- [x] **Read path finalized.** `CreateAsync` returns the recovery **words**, because creation is
  the only point they are available without re-authorizing - reading them back goes through
  protected storage and prompts the user, which would be absurd immediately after creating the
  wallet they are about to be shown. `RevealRecoveryPhraseAsync` returns the same shape, and the
  service splits once on any whitespace so no caller does it.
- [x] **The projection now carries what the engine knows.** It was 3 of the snapshot's 10 fields,
  which meant a failed refresh looked exactly like an empty wallet. `WalletState` now also carries
  the account status, the per-resource phase and error (`WalletResource`, with `CanRetry` from the
  engine's `RetryAdvice` - so a view cannot offer a retry the engine says is pointless), the
  activity list, its own resource, and `HasMoreActivity`. `LoadMoreActivityAsync` is the operation
  that implies. `Uninitialized` is documented as *not* empty, since that cost an afternoon.
- [ ] **Deliberately still unsurfaced:** send (`PreviewSend`/`Send`/`CancelSend`/`SignMessage`),
  NFTs (`RefreshNfts`/`LoadMoreNfts`/transfers), TON Connect (`TonConnectSession`), and
  `ResolvePending` as a public operation. Each is a feature with no UI behind it yet, and guessing
  at their shape before there is one is how the last service ended up a mess.
- [ ] **Left alone deliberately:** `Libraries/ton-walletkit-core` is still on disk - it is its own
  checkout, untracked here, and not mine to delete. `WalletTestPopup` is still constructed by
  nothing, but it is the "test" step of the create flow rather than dead code.
- [ ] **Not yet done, and known:** nothing has *run* beyond the bugs above. `WalletTestPopup` is constructed by nothing
  and looks like spike leftovers; `WalletBackupViewModel` still carries a commented-out block
  referencing the deleted API. The send, activity and NFT halves of `WalletClient` are not
  surfaced at all — only balance, import, reveal and delete are wired.

### Remaining items

- [ ] **4.1** Pick the project. The generated file is one large `.cs`, and
  **`Telegram.csproj` is not globbed** (~1240 explicit `Compile` entries) — a generated file
  without an entry is silently not built. `Telegram.Modern.csproj` globs, so it needs
  nothing.
- [ ] **4.2** Implement the seven host callbacks. `WalletHttpHost`: `execute_http` and
  `cancel_http` — the host owns the timeout, must reject redirects, and must return the
  observed `final_url` or the engine treats the response as a policy violation.
  `WalletPlatformHost`: `read_protected_secret`, `store_protected_secret`,
  `delete_protected_secret`, `load_journal`, `compare_exchange_journal` — the last is a
  durable compare-and-swap in one transaction, not a read-then-write.
- [ ] **4.3** Threading. The engine calls hosts from its own threads and explicitly does not
  hold the wallet-state lock across a callback; nothing in our implementations may assume
  the UI thread.
- [ ] **4.4** Secret hygiene. Both SWIFT.md and KOTLIN.md warn that an immutable string
  cannot be cleared, so `RecoveryPhrase.phrase` must not outlive the recovery screen and
  must never reach logs, errors, analytics or saved state. C# `string` has the same
  property; mutable copies in the platform host should be `byte[]` cleared in a `finally`.
- [x] **4.5** **Decided: WalletKit is out.** Fela's colleague at TON Org says WalletKit will
  not be supported for this, so `Libraries/ton-walletkit-core` — the QuickJS embedding of
  `@ton/walletkit` with its C#/Java bindings — and everything built on it is retired.

  It is a smaller loss than it sounds, because it never reached `develop`. The work is a
  **single commit**, `f6d3bf663 "Wallet POC"` (2026-08-07), on the local branch **`wallet`**:
  50 files, 5,756 insertions, **1 ahead of develop and 364 behind**. (`origin/ton` is
  unrelated — 9,240 behind.) The branch needs rebasing regardless of this decision.

  **The POC is throwaway apart from the UI** (Fela's call). Nothing below the view layer needs
  preserving, so the service and host code is written fresh against wallet-engine rather than
  ported.

  | | |
  |---|---|
  | **Keep** | the XAML and UI — `WalletPage`, `WalletBackupPage`, `WalletImportPopup`, `WalletSharePopup`, `WalletTestPopup`, ~2,700 lines; plus `WalletCardDemo` (~1,300 lines and the HLSL shader), which never touched WalletKit |
  | **Throw away** | `WalletService.cs` (652 lines), `TonWalletHost.cs` (375), `TonProtectedStore.cs` (224), and `Libraries/ton-walletkit-core` entirely |

  Writing fresh is the right call and not just a preference — the two host contracts differ
  where it counts, so a port would inherit shape without saving work. wallet-engine wants a
  versioned **compare-and-swap journal**, and `TonProtectedStore` is a flat string KV store
  with no atomic compare-exchange; it wants a `RequireUserPresence` policy on secret reads; and
  its HTTP host must enforce the timeout, reject redirects and report the observed `final_url`
  or the engine treats the response as a policy violation. The one idea worth carrying over as
  an *idea* rather than as code is `TdlibHttp` — the POC routed wallet HTTP through TDLib
  rather than the system stack, which is a deliberate choice worth making again.

  The UI's only tie to the old core is `TonAmount` (`WalletViewModel.Balance`,
  `Formatter.TonBalance`) — a 50-line `readonly struct` over `BigInteger`. wallet-engine
  returns `UnsignedDecimalString BalanceNanograms`, a decimal string of nanograms, so whatever
  replaces it takes a string and the UI is unaffected either way.

## Task 5 — Parked, not forgotten

- [ ] **5.1** `Task.WaitAsync` in the generated foreign-callback path is behind
  `#if NET6_0_OR_GREATER`. On `netstandard2.0` the cancellation branch silently compiles
  away, so a Rust future that gets dropped will not cancel our in-flight HTTP request. Not a
  correctness bug — the completion callback is still invoked exactly once on every path —
  but it wastes a request and holds a connection. Revisit once something is running.
- [x] **5.2** **.NET Native runs the generated bindings. Proven, not argued.** Spike at
  `C:\Source\WalletEngineSpike` (`Run.ps1`, README explains the probes), a UWP app built
  `Release|x64` with `UseDotNetNativeToolchain`, modelled on DustSpike.

  The AOT toolchain compiled all 13,911 generated lines without complaint — application
  closure, interop code generation, native code, exit 0, no marshalling errors. Then, running
  on **`.NET Native 4.6.29511.0`** with dynamic code unavailable, all three probes passed:

  | Probe | Result |
  |---|---|
  | Forward call + record lift (`ParseTonConnectManifest`) | parsed; compiler-generated record `ToString` walks every field |
  | Error return (bad JSON) | arrived as a thrown `TonConnectSessionException` variant carrying the Rust diagnostic, not a crash |
  | **Vtable registration + async callback** (`WalletLifecycle.CreateWallet`) | derived a real testnet address, 24-word phrase, and the in-memory `WalletPlatformHost` was called back from a Rust thread |

  The third is the one that mattered: constructing the object registers a vtable of managed
  callbacks with Rust, and `CreateWallet` calls into C# **from a Rust thread** and awaits the
  `Task` the callback returns — `Marshal.GetFunctionPointerForDelegate` (19 sites),
  `GetDelegateForFunctionPointer<T>` (14), 28 delegate types and 173 `DllImport`s, all under
  AOT. The probe asserts the host was actually called, so a silent no-op could not have passed.

  Two things that helped and are worth keeping:
  - `GetDelegateForFunctionPointer<T>` is instantiated over exactly **two concrete, statically
    visible types**, so there are no open generics for AOT to fail on.
  - The generator's one reflection site — `FFIObjectUtil.Dispose`, which walks runtime types to
    dispose collections — is **dead code for this API**: zero call sites, and no record
    contains object references. No `rd.xml` entries were needed.

  `LangVersion 14.0` and `PolySharp` mirror `Telegram.csproj`; the generated code uses records
  and this BCL has no `IsExternalInit`. The spike is registered on the dev machine as
  `WalletEngineSpike_k580v1fv7e4c6`.
- [ ] **5.3** Upstream the C# generator work or keep the fork. TON Core already vendors a
  NordSecurity generator and labels C++ "experimental", so a C# backend would sit at the
  same tier and would probably be taken. Forking first and offering it once it is green
  costs nothing either way.
