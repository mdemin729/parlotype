<#
.SYNOPSIS
    Claude Code hook wrapper around check-localization.ps1 (ADR-064, plan phase 2).

.DESCRIPTION
    Wired to two events in .claude/settings.json:

      PostToolUse  after an edit that could touch UI copy — reports the problem
                   while the context that caused it is still in hand, which is
                   when fixing it is cheap.
      Stop         before the session ends — PostToolUse alone is not enough,
                   because a session can end mid-edit.

    Reads the hook payload on stdin. Exit 2 is the blocking code: stderr goes back
    to Claude as feedback.

    Deliberately conservative about its own failures. If the payload cannot be
    parsed or the checker itself blows up, this exits 0 — a broken guard must
    never wedge a session. The xUnit parity tests are the backstop that cannot be
    skipped.

.PARAMETER Event
    Which hook fired. Only affects the wording of the feedback.
#>
[CmdletBinding()]
param(
    [ValidateSet('PostToolUse', 'Stop')]
    [string]$Event = 'PostToolUse'
)

$ErrorActionPreference = 'Stop'

# Paths that can plausibly change user-facing copy. Everything else is skipped so
# an ordinary Core or Platform edit costs nothing.
$watched = @(
    '\.resx$'
    '\.axaml$'
    'src/Parlotype\.Desktop/ViewModels/'
    'src/Parlotype\.Desktop/Resources/'
    'src/Parlotype\.Core/Localization/'
)

try {
    $raw = [Console]::In.ReadToEnd()
    $payload = if ([string]::IsNullOrWhiteSpace($raw)) { $null } else { $raw | ConvertFrom-Json }
} catch {
    exit 0
}

if ($Event -eq 'Stop') {
    # The Stop hook can itself be the reason Claude keeps going. Never block twice.
    if ($payload -and $payload.stop_hook_active) { exit 0 }
} else {
    $path = $null
    if ($payload -and $payload.tool_input) { $path = $payload.tool_input.file_path }
    if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

    $normalized = $path -replace '\\', '/'
    $relevant = $false
    foreach ($pattern in $watched) {
        if ($normalized -match $pattern) { $relevant = $true; break }
    }
    if (-not $relevant) { exit 0 }
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$checker = Join-Path $repoRoot 'scripts/check-localization.ps1'
if (-not (Test-Path $checker)) { exit 0 }

try {
    $output = & pwsh -NoProfile -File $checker 2>&1
    $failed = $LASTEXITCODE -ne 0
} catch {
    exit 0
}

if (-not $failed) { exit 0 }

# Distinguish progress from regression. During the phase-3 extraction the common
# case is a file dropping below its recorded count, which is the work going well —
# telling the author they broke something would be both wrong and, repeated over a
# few hundred strings, actively misleading.
$findings = @($output | Where-Object { $_ -match '^\s+- ' })
$onlyProgress = $findings.Count -gt 0 -and -not ($findings | Where-Object { $_ -notmatch 'is down to' })

if ($onlyProgress) {
    $message = @"
Extraction progress — the baseline is now stale (nothing is broken).

$($output -join "`n")

Record it before finishing:
  pwsh scripts/check-localization.ps1 -UpdateBaseline
"@
} else {
    $lead = if ($Event -eq 'Stop') {
        'This session cannot end with a stale translation.'
    } else {
        'That edit left the translations out of step.'
    }

    $message = @"
$lead

$($output -join "`n")

Fix it now, in this change — a locale left behind is a broken build, not a follow-up task:
  1. add or update the key in src/Parlotype.Desktop/Resources/Strings.resx
  2. add the same key to EVERY Strings.<culture>.resx (see SupportedUiLanguages.All)
  3. pwsh scripts/gen-strings.ps1
  4. pwsh scripts/check-localization.ps1

New UI copy belongs in resx and is bound with {loc:Tr Key}, never written inline in AXAML.
See .claude/skills/localization/SKILL.md.
"@
}

[Console]::Error.WriteLine($message)
exit 2
