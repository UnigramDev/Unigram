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

These figures are for deciding what to work on. They are private telemetry and never get
published — see section 5.

Prefer groups that are frequent, recent, on the current release, and whose `SYMS` is high.
Skip a group whose own-code PDB is missing — you cannot analyse what you cannot symbolicate.

## 2. Symbolicate

```
$CRASHCTL show <group_hash> --sample 60 --log-tail
```

Always pass `--log-tail`. It is the highest-value part of the record and costs one extra
request. Read the output in this order:

- **`versions`** — which releases crash. Carry this into section 3.
- **`frames N/M resolved`** and **`!! MISSING SYMBOLS`** — if the Unigram modules are missing,
  stop and report; only OS modules missing is usually fine.
- **The frames** — find the topmost frame in Unigram code. That is your anchor.
- **`exit point`** — the managed trace as the backend reported it. Because the app ships
  .NET Native (AOT), symbol names are mangled (`$0_Telegram::…`, `Stub_18<System.__Canon>`);
  `exit_point` is often the more readable rendering of the same stack. The symbolicator's
  added value over it is the file and line.
- **`log tail`** — see below.

`--json` for structured output, `--offline` to skip the Microsoft symbol server when triaging
in bulk.

### The log tail is where the cause usually is

The stack says *where* the app died. The log tail says *what it was doing*, which is what you
actually need to explain the crash. It has two parts.

A **state header** — app version, language, session duration, memory used/available/total,
window size, column width, screen scaling and text scaling, active calls, and often the raw
`HRESULT`. Check these against the crash's device spread: an OOM-shaped crash with
`Memory available` near zero, or a layout crash at 200% scaling, is a much stronger lead than
the stack alone.

A **timestamped trace**, each line `[unixtime,ms][File.cs:line][Method] message`:

```
[…077][NavigationService.cs:467][NavigateToAsync] Mode: New, Parameter: 8870522043 …
[…087][InstantContent.xaml.cs:183][UpdateView] Steps: 4, added: 1, removed: 3, moved: 0
[…106][InstantContent.xaml.cs:183][UpdateView] Steps: 6, added: 1, removed: 5, moved: 0
```

Work backwards from the last entries. Look for the sequence that reaches the anchor frame,
repeated or escalating values, and how much wall-clock time the last steps took. These lines
carry their own `File.cs:line`, so they corroborate the symbolicated stack independently — if
the two disagree, trust neither until you understand why.

Logging in the app is currently sparse, so the trace often thins out right before the
interesting moment. When that happens, note in the PR which log line would have settled the
question; adding it is a legitimate follow-up.

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

Reconcile three sources before settling on a cause: the symbolicated stack (where), the log
tail's final entries (what led there), and the code at the reference commit (how that state is
reachable). A diagnosis that explains all three is worth acting on; one that explains only the
stack usually is not.

**Find out why the value is wrong before deciding what to do about it.** "It was null" is where
the investigation starts, not where it ends. Trace back to the code that should have set it and
establish what actually happened: who assigns it, under what conditions that assignment is
skipped, and which of those the crash reports match. Only then is there enough to choose a fix.

The bar is that you can name the state that produced it in one sentence — "the container was
connected before its content was set", "the selector hands both item types the same template" —
and point at the code that creates that state. If the best available sentence is "somehow it
was null", the diagnosis is not finished, and adding a null check just moves the failure
somewhere quieter without removing it.

Where the answer lands decides the fix, and the three outcomes are different:

| Why it was null | Fix |
|---|---|
| The API legitimately returns null and the caller assumed otherwise | Handle it at the call site — a guard is genuinely correct |
| Something upstream is misconfigured or wired wrong | Fix that. A guard here hides a permanent bug and usually degrades silently |
| Two things race, so the state is merely not there *yet* | Sequencing. Neither defaulting nor returning early is right |

All three appeared in this codebase within a day: `GetSelectedSourceText` documents three null
returns and the caller ignored them (guard was right); `AutocompleteTemplateSelector` returned
one template for two item types (guard was wrong, the selector was); `WindowContext` was absent
because activation beat window creation (guard and construction were both wrong).

A guard that is added without knowing which row applies is a guess, and two of the three rows
punish it.

**Read the mechanism, don't infer it.** A hypothesis that explains the stack is not the same as
the cause, and it is easy to build a confident fix on one that was never checked. Before
writing code, name the specific file that would confirm it and open that file.

Two checks that catch most of it:

- **Does the frequency match the story?** A race should crash intermittently; a wrong constant
  or a mismatched template crashes every single time that path runs. If the theory is a race
  but the failing path is rare-but-deterministic, the theory is wrong.
- **For a visual-tree mismatch, read the `DataTemplateSelector` before blaming recycling.**
  Two item types mapped to one `DataTemplate` while the code-behind expects different trees is
  a permanent mismatch, and the fix belongs in the template or the selector, not in a guard
  around the symptom. Guarding it there converts a crash into a silently blank row and hides
  the real bug. This exact mistake sank a PR: `AutocompleteTemplateSelector` returned
  `CommandTemplate` for both `UserCommand` and `QuickReplyShortcut`, which one file would have
  shown.

**Never construct something to make a null go away until you know who else constructs it.**
Where the null is a not-yet-initialised singleton, creating one here means two exist: whichever
runs last wins the field, and everything written to the loser is silently discarded. That is
worse than the crash, because it corrupts state instead of aborting. Grep every construction
site and work out the ordering first. `WindowContext` is exactly this — the constructor assigns
the thread-static `_current`, and `OnWindowCreated` constructs one on its own schedule, so
building a second during activation orphans the frame that was just attached to the first.

**When the timing is the bug, the fix is sequencing, not defaulting.** If state is missing
because two things race, neither inventing the state nor returning early is right: returning
leaves the feature silently dead (the same trap as guarding a template mismatch). What is
needed is to carry the input forward and act on it when the state does arrive. That is a design
decision about startup, not a null check — propose it and ask rather than guessing.

If you cannot explain the crash, say so and stop. A plausible-looking guess wastes more time
than an honest "not diagnosed". That includes the case where a guard would obviously stop the
crash: shipping one without knowing why the value was wrong is still a guess, and it costs the
next person the evidence, because the crash stops being reported while the bug is still there.

Say what the cause was in the PR, not only what the change does. A reviewer who knows this code
can tell in seconds whether the stated cause is right, and that is the check worth optimising
for — far more than whether the diff looks tidy.

## 5. Fix and open a PR

The fix ships from current code, not from the old release, so branch off `develop` — but off
**GitHub's** develop, not a local mirror of it. A clone of the user's working repo inherits their
remote-tracking refs, which are routinely stale *and* carry unpushed local commits, so
`origin/develop` there is not the branch the PR will merge into:

```
git -C <workdir> remote add github https://github.com/UnigramDev/Unigram
git -C <workdir> fetch github develop
git -C <workdir> checkout -b <short-slug> github/develop
```

Do not prefix the branch with `fix/`: a branch literally named `fix` already exists on the
remote, and git rejects the push with `directory file conflict`.

**First re-check the bug on `develop`.** The code may have moved or already been fixed. If it
is already fixed, stop and report that — including which commit fixed it, so the user can
decide whether the crash group can be closed after the next release.

Then make the change. Keep it minimal and targeted at the diagnosed cause; a crash fix is not
an invitation to refactor. Follow `CLAUDE.md` — these are often hot paths, so no new
allocations in rendering/layout, and no lambda event handlers.

Open the PR against `develop`:

```
git -C <workdir> push -u github <short-slug>
gh pr create --repo UnigramDev/Unigram --base develop --head <short-slug> --title "..." --body "..."
```

The PR body should carry: the error type and message, the versions affected, the symbolicated
frames that identify the site, the diagnosis, and what the fix changes. That is what makes the
PR reviewable without re-running the tooling.

**Never publish impact numbers.** The repository is public; how many users a crash affects is
private telemetry and must not appear in a PR body, a commit message, a branch name or an issue
— not as a user count, not as a device count, not as "30+ distinct devices", and not as a
crash count that implies one. Group hashes and per-device/per-OS breakdowns stay out too. Use
the numbers to decide what to work on, never to justify the work in public.

Don't strip by hand — that already failed once. Generate the public text mechanically:

```
$CRASHCTL show <group_hash> --public
```

It emits the exception, the frames and the missing-symbol list with all of the above withheld,
overrides `--json`/`--log-tail` so neither reintroduces it, and rewrites account names in
Windows user paths. Say "reported by crash telemetry on <version>" and stop there.

This applies to the commit message as much as the PR body — it is pushed and equally public.
Check both before pushing; scrubbing after the fact needs a force-push.

**State plainly whether the change was built and run.** A UWP/.NET Native build is usually not
available here, and a reviewer must not have to guess whether a patch was compiled. Roslyn will
at least confirm the file still parses:

```
CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetDiagnostics()
```

That catches typos, not type errors — say so rather than implying more.

## 6. Close the group when the PR merges

Merging is what marks a crash fixed:

```
$CRASHCTL mark <group_hash> --status closed --yes
```

The group hash cannot live in the PR (section 5), so record the mapping locally when you open
the PR — `<pr number> <tab> <group hash>` in a scratch file — or the link is lost the moment
the PR is the only thing left.

Merged is not released, so the group can still collect crashes from users on the old build.
That is expected; the status reflects that a fix exists, not that it has reached anyone yet.

To notice the merge, watch the PR:

```
gh pr view <pr> --repo UnigramDev/Unigram --json state -q .state
```

A polling watcher must filter out its own comments. `gh` posts under the user's account, so
author cannot distinguish them, and unfiltered the agent reads its own words back as if they
were instructions. End every agent-written comment with an HTML-comment marker and skip
comments containing it.

## Limits
- **Never push to `develop` or `main`,** and never force-push. Fixes go on a branch, via a PR.
- **Never commit in `C:\Source\Telegram`.** All work happens in the clone.
- **Crash data is user data, and the repository is public.** Records carry device names and OS
  builds, and the backend returns a `user_id` (crashctl never prints it). Keep output local —
  it does not go into artifacts, gists, or anything published. Quoting a stack trace is fine.
  Impact numbers, device lists and group hashes are not: see section 5.
- **Report honestly.** "Symbols missing, could not analyse" and "could not determine the cause"
  are correct outcomes. Do not upgrade a hypothesis to a diagnosis to have something to show.
