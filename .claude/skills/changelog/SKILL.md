---
name: changelog
description: Write the periodic Telegram-channel changelog of recent work, in Fela's established style. Use when asked for a changelog, a summary of what was done since a date, or "what did we ship this week".
---

# Changelog

Fela posts a short changelog every few days. **The audience is management**: technical
readers who follow the project but do not read the code. One line per piece of work, many
commits collapsed into a single line, no commit hashes, no file names, no class names.

Each line answers *what area moved, and what is now true that was not before*. Name the
subsystem a manager already knows — the composer, media sharing, the build, calls, crashes
— and the outcome. Not the type that changed, not the mechanism, unless the mechanism *is*
the deliverable (a rewritten parser, a new code generator).

The input is a date range. If none is given, ask — or infer it from the timestamp of the
previous entry the user pastes in (the previous entry's day is normally *included* again,
since it was written mid-day).

## 1. Collect the commits

The work is spread over several repositories. Scan all of them for the range, not just the
app:

```
git -C C:/Source/Telegram log --since=<from> --until=<to> --date=short --pretty=format:'%h %ad %s'
```

Then the submodules and the sibling repos that belong to this project:

```
Libraries/tdlib   Libraries/tgcalls   Libraries/ton-walletkit-core
C:/Source/tlottie   C:/Source/RLottie.UWP   C:/Source/deps   C:/Source/vlc
C:/Source/UnigramUtils   C:/Source/Dashboard   C:/Source/CrashServer
```

Also check `git worktree list` — substantial work often lives on a branch in its own
worktree and never appears in the main tree's log.

Finally check `git status` and the mtimes of untracked docs and project files. Uncommitted
work counts if it is real progress, but say so when handing the draft over so Fela can drop
the line if it is too early to announce.

To see what a cluster of commits actually touched:

```
git log --since=<from> --numstat --pretty=format:'@@%h %s' | awk '/^@@/{c=$0} /^[0-9]/{split($0,a,"\t"); f[a[3]]+=a[1]+a[2]} END{for (k in f) print f[k], k}' | sort -rn | head -40
```

## 2. Group

One line per *piece of work*, not per commit. A run of twenty commits on one popup is one
line. The grouping is by feature as a user would name it — the rich editor, the composer,
SendFilesPopup, the build, calls, crashes.

Recurring shapes:

- **Crash fixes** collapse to a count and an issue range: `17 crash fixes (#3338–#3359)`.
  Count the commits carrying a `(#NNNN)` suffix; the range is first to last.
- **A review** (call managers, ClientService, the WebRTC fork) is one line naming the thing
  reviewed and the two or three findings worth mentioning.
- **A performance pass** is one line naming the surface and what stopped happening.
- **Releases** get their own line: `released 12.8.1 with bug fixes`.
- Skip pure hygiene: merges, typo fixes, review-doc checkbox updates, commit-message
  reflows, "write down where X stands" notes.

## 3. Style

Match the existing entries exactly:

- plain `- ` bullets, no nesting, no bold, no headings
- lowercase first word, no trailing period
- noun phrase or past tense, never "we" and never "you"
- **one line, and it has to fit on one line** — around ten words, never wrapping in the
  post. This is the rule that is easiest to break and the one that shows most: a bullet
  that runs to two lines is a paragraph pretending to be a bullet. Cut the qualifiers,
  keep the noun.
- 8–18 lines for a few days of work; more than 20 means the grouping is too fine
- an em dash appends the detail to the topic, at most three items and no clauses:
  `selection fixes — start from the bubble surface, direct hit only, double click`
- third-party and component names a manager would recognise are fine (TDLib, libvlc,
  WebRTC, vcpkg, .NET 10); internal type names are not — `SendFilesPopup` is "media
  sharing", `FormattedTextBox` is "the composer", `WASAPI output` is "audio output"
- numbers only when they are solid and were measured — `2–3× faster`, not `much faster`.
  Crash *impact* figures (affected users, device counts) are private telemetry and never
  go in a public post.

Reference entries, for tone:

```
- new unsupported message
- buttons in rich messages
- add x64 and windows support to tlottie
- created Claude skill to automatically fix crashes from user reports
- fixed deadlock in HEVC 10-bit playback
```

```
- service messages split into one control per type, recycled, plus a base for community add/remove
- selection fixes — start from the bubble surface, direct hit only, double click
- call manager review — out-of-bounds write, E2E frame handling, loopback capture rewritten
- 14 crash fixes (#3323–#3336)
```

## 4. Deduplicate

If the previous entry is available, read it before writing. The first day of the range is
usually shared with it, so its morning commits are already published — leave them out and
say which ones you dropped and why.

## 5. Hand over

Output the bullets in a fenced block so they can be copied into Telegram unchanged. Below
the block, note anything Fela has to decide: work that is uncommitted, work that is not yet
announced publicly, or a line whose numbers you could not verify.
