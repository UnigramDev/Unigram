#Requires -Version 7
<#
    Stop hook: releases the git lock this session holds, so the next session is not made
    to wait out the timeout. Leaves a lock belonging to anyone else alone.
#>

$ErrorActionPreference = 'Stop'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $payload = $raw | ConvertFrom-Json
    $sessionId = $payload.session_id
    $cwd = if ($payload.cwd) { $payload.cwd } else { (Get-Location).Path }

    # The guard locks whichever worktree a command ran in, so look in all of them.
    $commonDir = & git -C $cwd rev-parse --path-format=absolute --git-common-dir 2>$null
    if (-not $commonDir) { exit 0 }

    $lockPaths = @(Join-Path $commonDir 'claude-git-lock.json')
    $worktrees = Join-Path $commonDir 'worktrees'
    if (Test-Path $worktrees) {
        $lockPaths += Get-ChildItem $worktrees -Directory | ForEach-Object { Join-Path $_.FullName 'claude-git-lock.json' }
    }

    foreach ($lockPath in $lockPaths) {
        if (-not (Test-Path $lockPath)) { continue }

        try {
            $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
            if ($lock.session -eq $sessionId) {
                Remove-Item $lockPath -Force
            }
        }
        catch {
            # Someone else's or unreadable: leave it to expire.
        }
    }
}
catch {
    # A lock left behind expires on its own; never fail the turn over it.
}

exit 0
