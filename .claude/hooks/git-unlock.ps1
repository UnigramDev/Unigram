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

    $gitDir = & git -C $cwd rev-parse --absolute-git-dir 2>$null
    if (-not $gitDir) { exit 0 }

    $lockPath = Join-Path $gitDir 'claude-git-lock.json'
    if (-not (Test-Path $lockPath)) { exit 0 }

    $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
    if ($lock.session -eq $sessionId) {
        Remove-Item $lockPath -Force
    }
}
catch {
    # A lock left behind expires on its own; never fail the turn over it.
}

exit 0
