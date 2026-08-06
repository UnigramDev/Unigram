---
name: crash-triage
description: Triage and fix production crashes reported by the Unigram crash dashboard. Use when asked to look at crashes, investigate a crash group, find out why users are crashing, or fix a reported crash. Covers fetching crash groups, symbolicating stack traces, mapping a crashing version back to its source, and opening a fix PR.
---

# Crash triage

Production crashes land on the `ugram_crash_logs` backend. `crashctl` fetches them and turns
addresses into `file.cs:line` using the PDB store shared with the UWP Dashboard.

**The single biggest failure mode is reading the wrong source.** Crash line numbers refer to the
*released build*, not to whatever is checked out. Section 3 exists to prevent that, and it is
not optional.

## 0. Setup

```
CRASHCTL=C:/Source/Dashboard/Dashboard.Cli/bin/Release/net9.0/win-x64/crashctl.exe
```

Build it if missing: `dotnet build C:/Source/Dashboard/Dashboard.Cli -c Release`.

The API token is not in the binary. It resolves from `UGRAM_CRASH_TOKEN`, `--token`, or
`%LOCALAPPDATA%\crashctl\token` — write it to that file once. If none is set, crashctl says so
and points at where the token lives; ask the user rather than digging it out of git history.

Never work in `C:\Source\Telegram` itself. It is the user's working tree and is usually dirty.

## 1. Pick a target

```
$CRASHCTL list --days 7 --sort users --limit 20
```

Columns: group hash, count, affected users, version, `SYMS`, type/message. `SYMS` is
`cached/total` PDBs — a group showing `2/9` will need downloads or will leave frames
unresolved. `--sort users` ranks by people affected, which is usually the right priority;
`--sort count` ranks by raw volume.

Prefer groups that are frequent, recent, on the current release, and whose `SYMS` is high.
Skip a group whose own-code PDB is missing — you cannot analyse what you cannot symbolicate.

## 2. Symbolicate

```
$CRASHCTL show <group_hash> --sample 60
```

Read the output in this order:

- **`versions`** — which releases crash. Carry this into section 3.
- **`frames N/M resolved`** and **`!! MISSING SYMBOLS`** — if the Unigram modules are missing,
  stop and report; only OS modules missing is usually fine.
- **The frames** — find the topmost frame in Unigram code. That is your anchor.
- **`exit point`** — the managed trace as the backend reported it. Because the app ships
  .NET Native (AOT), symbol names are mangled (`$0_Telegram::…`, `Stub_18<System.__Canon>`);
  `exit_point` is often the more readable rendering of the same stack. The symbolicator's
  added value over it is the file and line.

`--json` for structured output, `--log-tail` for the app log, `--offline` to skip the Microsoft
symbol server when triaging in bulk.

## 3. Map the version to its source — do not skip

A crash reported against `12.8.1.0` must be read at 12.8.1's code.

**Find the reference commit.** Try the tag first; the trailing `.0` is not part of the tag name:

```
git ls-remote --tags origin "v12.8.1"
```

Tags are not always pushed. When there is no tag, find the version bump — the version is always
bumped immediately before a release, so the bump commit is effectively the release tree:

```
git log --oneline --all -S"12.8.1" -- Telegram.Msix/Package.appxmanifest
```

Take the `Bump version to 12.8.1` commit. (Tags point at these same bump commits, so the two
routes agree.)

**Clone at that ref.** A local clone is fast and fully isolated from the user's working tree:

```
git clone C:/Source/Telegram <workdir> --no-checkout
git -C <workdir> checkout <tag-or-commit>
```

**Verify the anchor. This is the check that makes the rest trustworthy.** Open the crashing
file at the reference commit and confirm the reported line actually falls inside the reported
function:

```
git -C <workdir> show <ref>:Telegram/Controls/.../InstantContent.xaml.cs | sed -n '2335,2350p'
```

If line 2341 lands inside `SpacingBetweenBlocks`, the mapping is sound. If it lands somewhere
unrelated — or the file is shorter than the line number — **the mapping is wrong. Stop and say
so.** Do not fall back to guessing from the function name.

## 4. Diagnose

Read the anchor frame and its callers at the reference commit. State the failure concretely:
which value is null, which index is out of range, which invariant broke, and the sequence that
gets there. Use the version/OS/device spread from section 2 — a crash confined to one OS build
or one GPU vendor is a different bug from one spread evenly.

If you cannot explain the crash, say so and stop. A plausible-looking guess wastes more time
than an honest "not diagnosed".

## 5. Fix and open a PR

The fix ships from current code, not from the old release, so branch off `develop`:

```
git -C <workdir> fetch origin develop
git -C <workdir> checkout -b fix/<short-slug> origin/develop
```

**First re-check the bug on `develop`.** The code may have moved or already been fixed. If it
is already fixed, stop and report that — including which commit fixed it, so the user can
decide whether the crash group can be closed after the next release.

Then make the change. Keep it minimal and targeted at the diagnosed cause; a crash fix is not
an invitation to refactor. Follow `CLAUDE.md` — these are often hot paths, so no new
allocations in rendering/layout, and no lambda event handlers.

Open the PR against `develop`:

```
git -C <workdir> push -u origin fix/<short-slug>
gh pr create --repo UnigramDev/Unigram --base develop --title "..." --body "..."
```

The PR body should carry: the crash group hash, error type and message, affected users and
count, the versions affected, the symbolicated frames that identify the site, the diagnosis,
and what the fix changes. That is what makes the PR reviewable without re-running the tooling.

## Limits

- **Never mark a crash group closed.** `crashctl mark` writes to the shared backend and needs
  `--yes`. Whether a fix worked is only knowable after a release ships; that call is the
  user's.
- **Never push to `develop` or `main`,** and never force-push. Fixes go on a branch, via a PR.
- **Never commit in `C:\Source\Telegram`.** All work happens in the clone.
- **Crash data is user data.** Records carry device names and OS builds, and the backend
  returns a `user_id` (crashctl never prints it). Keep output local — it does not go into
  artifacts, gists, or anything published. Quoting a stack trace in the PR is fine; pasting
  device lists is not.
- **Report honestly.** "Symbols missing, could not analyse" and "could not determine the cause"
  are correct outcomes. Do not upgrade a hypothesis to a diagnosis to have something to show.
