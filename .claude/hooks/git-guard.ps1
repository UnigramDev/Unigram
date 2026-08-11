#Requires -Version 7
<#
    PreToolUse guard for git write commands.

    Several pieces of work share this checkout at once — Fela's own edits plus one or
    more Claude sessions — and they all share one index and one HEAD. That makes a
    handful of ordinary git commands quietly destructive:

      * `git add -A` sweeps up whatever else happens to be dirty.
      * a bare `git commit` commits the whole index, including what someone else staged.
      * `git reset --hard` / `git clean -f` throw away another session's uncommitted work.

    This blocks those, and serialises staging and committing behind a per-worktree lock
    so two sessions cannot interleave a stage/commit pair.

    Reads the hook payload as JSON on stdin. Any failure in here allows the command
    through: a broken guard must not be able to halt all work.
#>

$ErrorActionPreference = 'Stop'

# How long a lock stays valid without being refreshed. Long enough to cover a slow
# stage-then-commit, short enough that an abandoned session unblocks the other one.
$LockTimeoutSeconds = 900

function Approve {
    exit 0
}

function Deny([string]$reason) {
    $verdict = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
    $verdict | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { Approve }

    $payload = $raw | ConvertFrom-Json
    $command = $payload.tool_input.command
    if ([string]::IsNullOrWhiteSpace($command)) { Approve }
    if ($command -notmatch '\bgit\b') { Approve }

    $sessionId = if ($payload.session_id) { $payload.session_id } else { 'unknown' }
    $cwd = if ($payload.cwd) { $payload.cwd } else { (Get-Location).Path }

    # Blank every literal before anything is parsed. A commit message is the common case
    # and it can hold newlines, semicolons and the word "git" — none of which are
    # commands. Here-strings first, since their bodies contain bare quotes.
    $masked = [regex]::Replace($command, "@'.*?'@", "''", [Text.RegularExpressions.RegexOptions]::Singleline)
    $masked = [regex]::Replace($masked, '@".*?"@', '""', [Text.RegularExpressions.RegexOptions]::Singleline)
    $masked = [regex]::Replace($masked, '"[^"]*"', '""', [Text.RegularExpressions.RegexOptions]::Singleline)
    $masked = [regex]::Replace($masked, "'[^']*'", "''", [Text.RegularExpressions.RegexOptions]::Singleline)

    # Only a line whose first token is git is treated as a git command.
    $segments = $masked -split '(?:&&|\|\||;|\r?\n)' |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -match '^git(\.exe)?\s' }

    if ($segments.Count -eq 0) { Approve }

    function Unquote([string]$text) { $text }

    $needsLock = $false

    foreach ($segment in $segments) {
        $bare = Unquote $segment

        # `git -C <path> …` — strip so the verb is always the next token.
        $verbPart = $bare -replace '^git(\.exe)?\s+(-C\s+\S+\s+)?', ''

        if ($verbPart -match '^add\b') {
            if ($verbPart -match '(\s|^)(-A|--all|-u|--update)(\s|$)' -or
                $verbPart -match '(\s)(\.|:/|\*)(\s|$)') {
                Deny (@(
                    'Refusing a sweeping `git add`: this checkout is shared, so -A / -u / . picks up'
                    'whatever else is dirty right now — another session''s edits, rebuilt .dll files,'
                    'submodule pointers.'
                    ''
                    'Name every path instead:  git add <path> <path>'
                ) -join "`n")
            }
            $needsLock = $true
        }

        if ($verbPart -match '^(rm|mv)\b' -or $verbPart -match '^apply\b.*--cached' -or
            $verbPart -match '^(restore|reset)\b.*--staged') {
            $needsLock = $true
        }

        if ($verbPart -match '^(reset\s+--hard|clean\s+-[a-zA-Z]*f)') {
            Deny (@(
                'Refusing a destructive reset/clean: this checkout is shared, and it would throw away'
                'uncommitted work belonging to another session or to Fela.'
                ''
                'If you genuinely need it, ask first and let a human run it.'
            ) -join "`n")
        }

        if ($verbPart -match '^commit\b') {
            $needsLock = $true

            # -a / -am, but not --amend or --all-of-something-else.
            if ($verbPart -match '\s-(?!-)[a-zA-Z]*a') {
                Deny (@(
                    'Refusing `git commit -a`: it commits every tracked file that is dirty, which in this'
                    'shared checkout means other people''s work too.'
                    ''
                    'Name the paths:  git commit -m "…" -- <path> <path>'
                ) -join "`n")
            }

            $hasPathspec = $verbPart -match '\s--(\s|$)' -or $verbPart -match '\s(-o|--only)(\s|$)'
            if (-not $hasPathspec) {
                Deny (@(
                    'Refusing a bare `git commit`: it commits the whole index, and this checkout is shared —'
                    'anything Fela or another session had staged goes in with it. That has already happened'
                    'once.'
                    ''
                    'Name the paths:  git commit -m "…" -- <path> <path>'
                    ''
                    'Note this commits the working-tree content of those paths, so hunk-level staging via'
                    '`git apply --cached` no longer survives a commit. Split such work into separate edits,'
                    'or do it in its own worktree.'
                ) -join "`n")
            }

            # `git commit -- <paths>` takes the working tree, ignoring the index. If a named
            # path is partly staged, that silently commits more than was staged.
            $tail = ($verbPart -split '\s--(\s|$)', 2)[-1]
            $paths = $tail -split '\s+' | Where-Object { $_ -and $_ -notmatch '^-' -and $_ -ne '""' -and $_ -ne "''" }

            foreach ($path in $paths) {
                $staged = & git -C $cwd diff --cached --name-only -- $path 2>$null
                $unstaged = & git -C $cwd diff --name-only -- $path 2>$null
                if ($staged -and $unstaged) {
                    Deny (@(
                        "Refusing to commit '$path': it has both staged and unstaged changes."
                        ''
                        '`git commit -- <path>` commits the working-tree content, so the distinction you'
                        'staged would be lost and more would be committed than you chose.'
                        ''
                        'Either stage the rest and commit it all, or move the unrelated part out of the way'
                        'first.'
                    ) -join "`n")
                }
            }
        }
    }

    if (-not $needsLock) { Approve }

    $gitDir = & git -C $cwd rev-parse --absolute-git-dir 2>$null
    if (-not $gitDir) { Approve }

    $lockPath = Join-Path $gitDir 'claude-git-lock.json'
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

    if (Test-Path $lockPath) {
        try {
            $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
            $age = $now - [int64]$lock.at
            if ($lock.session -ne $sessionId -and $age -lt $LockTimeoutSeconds) {
                Deny (@(
                    "Another Claude session holds the git lock on this worktree (taken $age seconds ago)."
                    ''
                    'Staging and committing are serialised here because the index and HEAD are shared —'
                    'interleaving with another session corrupts both.'
                    ''
                    "Wait for it to finish, or work in your own worktree:"
                    "  git worktree add -b <branch> C:\Source\Telegram.worktrees\<branch> HEAD"
                ) -join "`n")
            }
        }
        catch {
            # An unreadable lock is treated as absent rather than blocking forever.
        }
    }

    @{ session = $sessionId; pid = $PID; at = $now } |
        ConvertTo-Json -Compress |
        Set-Content -Path $lockPath -Encoding utf8NoBOM

    Approve
}
catch {
    # Never let a fault in the guard stop work.
    Approve
}
