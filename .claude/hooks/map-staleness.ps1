#Requires -Version 7
<#
    SessionStart hook: report which sections of notes/architecture.md have drifted.

    The map is a hand-verified description of each subsystem, not a generated dump, so
    nothing keeps it honest on its own. Each section carries the commit it was last
    checked against:

        <!-- map: verified=<sha> paths=<comma-separated prefixes> -->

    This compares those paths against HEAD and names the sections whose files have moved
    since. It does not try to judge whether the description is still correct - only that
    it is unverified, which is the cue to read the source instead of trusting the map.

    Any failure in here exits silently: a broken hook must not be able to interrupt a
    session, and a missing map is the normal state of a fresh worktree.
#>

$ErrorActionPreference = 'Stop'
trap { exit 0 }

$root = $env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (Get-Location).Path }

$map = Join-Path $root 'notes/architecture.md'
if (-not (Test-Path -LiteralPath $map)) { exit 0 }

$sections = [System.Collections.Generic.List[object]]::new()
$name = $null

foreach ($line in [System.IO.File]::ReadAllLines($map)) {
    if ($line.StartsWith('## ')) {
        # Header is "## Name - paths (n files)"; keep only the name.
        $name = ($line.Substring(3) -split [char]0x2014)[0].Trim()
    }
    elseif ($line -match '^<!--\s*map:\s*verified=([0-9a-fA-F]{7,40})\s+paths=(\S.*?)\s*-->\s*$') {
        $paths = $Matches[2] -split ',' | ForEach-Object { $_.Trim().TrimEnd('/') } | Where-Object { $_ }
        if ($name -and $paths) {
            $sections.Add([pscustomobject]@{ Name = $name; Sha = $Matches[1]; Paths = $paths })
        }
    }
}

if ($sections.Count -eq 0) { exit 0 }

# One `git diff` per distinct SHA, not per section: every section carries the same SHA
# until it is individually refreshed, so this is normally a single git invocation.
$changed = @{}
foreach ($sha in ($sections.Sha | Sort-Object -Unique)) {
    $files = & git -C $root diff --name-only "$sha..HEAD" 2>$null
    # An unknown SHA (rebased away, shallow clone) yields nothing rather than a false alarm.
    if ($LASTEXITCODE -eq 0 -and $files) { $changed[$sha] = $files }
}

$stale = foreach ($section in $sections) {
    $files = $changed[$section.Sha]
    if (-not $files) { continue }

    $count = 0
    foreach ($file in $files) {
        foreach ($path in $section.Paths) {
            if ($file -eq $path -or $file.StartsWith($path + '/', [StringComparison]::Ordinal)) {
                $count++
                break
            }
        }
    }

    if ($count -gt 0) {
        [pscustomobject]@{ Name = $section.Name; Count = $count; Sha = $section.Sha.Substring(0, 7) }
    }
}

$stale = @($stale | Sort-Object Count -Descending)
if ($stale.Count -eq 0) { exit 0 }

$shown = $stale | Select-Object -First 12

$report = [System.Text.StringBuilder]::new()
[void]$report.AppendLine('notes/architecture.md is the map of this codebase - a subsystem index with entry points and traps. Read the relevant section before exploring an unfamiliar area.')
[void]$report.AppendLine('')
[void]$report.AppendLine('These sections are UNVERIFIED - files they describe changed after the section was last checked:')
foreach ($item in $shown) {
    [void]$report.AppendLine(('  - {0} - {1} file(s) changed since {2}' -f $item.Name, $item.Count, $item.Sha))
}
if ($stale.Count -gt $shown.Count) {
    [void]$report.AppendLine(('  - (+{0} more)' -f ($stale.Count - $shown.Count)))
}
[void]$report.AppendLine('')
[void]$report.AppendLine('Read the source rather than trusting a stale section. When a change alters what a section describes, update the section and its verified= SHA in the same commit.')

Write-Output $report.ToString()
exit 0
