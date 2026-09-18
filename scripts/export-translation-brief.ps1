<#
.SYNOPSIS
    Exports a compact JSON translation brief from Strings.resx (ADR-069).

.DESCRIPTION
    Half of the translation pipeline. Nobody edits a satellite resx by hand: a
    translator (human or agent) is handed this brief, returns a flat
    { "key": "translation" } map, and import-translations.ps1 turns that back
    into Strings.<culture>.resx.

    The brief carries, per key, the English value, the developer's <comment>
    where one exists, and the placeholder indices the translation has to
    preserve. That is everything a translator needs and nothing they do not,
    which matters when the reader is an LLM paying by the token.

    -Missing is the mode this script exists for beyond the initial 22-language
    push: after adding a key to Strings.resx, it emits a brief holding only the
    keys each locale is short, so a routine one-string change costs one small
    brief per language instead of a full 392-key pass.

.EXAMPLE
    pwsh scripts/export-translation-brief.ps1 -Culture de
    # Full brief for German, written to the temp directory (path is printed).

.EXAMPLE
    pwsh scripts/export-translation-brief.ps1 -Culture de -Missing -Out brief.json
    # Only the keys Strings.de.resx does not have yet.
#>
[CmdletBinding()]
param(
    # Target culture name, e.g. "de". Must be a row in SupportedUiLanguages.All
    # unless -Missing is off and the file does not exist yet (the first pass for
    # a new language runs before its registry row lands).
    [Parameter(Mandatory)]
    [string]$Culture,

    # Where to write the brief. Defaults to a temp path, printed on exit. The
    # brief is a working artefact and deliberately does not default into the
    # repo.
    [string]$Out,

    # Emit only the keys Strings.<culture>.resx is missing. Requires the file to
    # exist.
    [switch]$Missing,

    # Slice the brief: -Part 2 -Of 4 emits the second quarter of the keys.
    #
    # A translator's *reply* has an output-token ceiling, and 389 values in one
    # JSON object can exceed it — Finnish did, and the agent died before writing
    # a single byte. Slicing lets one translator answer in several files while
    # still seeing the whole job, which matters: splitting the work across
    # several translators instead would split the terminology with it.
    #
    # Keys keep the neutral file's order, so the slices concatenate back to the
    # whole with no gaps or overlap.
    [int]$Part = 0,
    [int]$Of = 0
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resourcesDir = Join-Path $repoRoot 'src/Parlotype.Desktop/Resources'
$neutralResx = Join-Path $resourcesDir 'Strings.resx'
$targetResx = Join-Path $resourcesDir "Strings.$Culture.resx"

if ($Culture -eq 'en') {
    throw "'en' is the neutral file itself (Strings.resx) — there is nothing to translate into it."
}

if (-not $Out) {
    $dir = Join-Path ([System.IO.Path]::GetTempPath()) 'parlotype-l10n'
    $null = New-Item -ItemType Directory -Path $dir -Force
    $Out = Join-Path $dir "$Culture.brief.json"
}

# ------------------------------------------------------------------ read resx

function Read-DataNodes([string]$path) {
    [xml]$doc = Get-Content -Path $path -Raw -Encoding UTF8
    $nodes = [ordered]@{}
    foreach ($entry in $doc.root.data) {
        if ($null -eq $entry.name) { continue }
        $nodes[$entry.name] = [pscustomobject]@{
            Value   = [string]$entry.value
            Comment = if ($entry.comment) { [string]$entry.comment } else { $null }
        }
    }
    return $nodes
}

$neutral = Read-DataNodes $neutralResx

$skip = @{}
if ($Missing) {
    if (-not (Test-Path $targetResx)) {
        throw "-Missing needs an existing $targetResx. Drop the switch for a first full pass."
    }
    foreach ($key in (Read-DataNodes $targetResx).Keys) { $skip[$key] = $true }
}

# ----------------------------------------------------------------- build brief

$entries = [System.Collections.Generic.List[object]]::new()

foreach ($key in $neutral.Keys) {
    if ($skip.ContainsKey($key)) { continue }

    $value = $neutral[$key].Value

    # Placeholder indices, distinct and ordered. The translation may move them
    # within the sentence but must use exactly this set — languages disagree
    # about word order, never about how many values a sentence takes.
    $placeholders = @(
        [regex]::Matches($value, '\{(\d+)') |
            ForEach-Object { [int]$_.Groups[1].Value } |
            Sort-Object -Unique)

    $entry = [ordered]@{
        key = $key
        en  = $value
    }
    if ($neutral[$key].Comment) { $entry.comment = $neutral[$key].Comment }
    if ($placeholders.Count -gt 0) { $entry.placeholders = $placeholders }

    $entries.Add([pscustomobject]$entry)
}

if ($entries.Count -eq 0) {
    Write-Host "Nothing to translate — Strings.$Culture.resx already covers every key in Strings.resx."
    return
}

$sliceNote = $null
if ($Of -gt 0) {
    if ($Part -lt 1 -or $Part -gt $Of) {
        throw "-Part must be between 1 and -Of ($Of); got $Part."
    }

    $total = $entries.Count
    # Ceiling division, so the last slice is the short one rather than a
    # surprise extra slice appearing after the caller has spawned -Of agents.
    $size = [math]::Ceiling($total / $Of)
    $skip = ($Part - 1) * $size
    $entries = [System.Collections.Generic.List[object]](
        $entries | Select-Object -Skip $skip -First $size)

    $sliceNote = "part $Part of $Of"
}

$brief = [ordered]@{
    culture         = $Culture
    sourceKeyCount  = $neutral.Count
    entryCount      = $entries.Count
    entries         = $entries
}
if ($sliceNote) {
    $brief.Insert(1, 'slice', $sliceNote)
}

# -Depth matters: the default of 2 silently truncates the entries array into
# type names, which looks like a working brief right up until a translator reads
# "System.Object[]".
$json = $brief | ConvertTo-Json -Depth 6

$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path $outDir)) { $null = New-Item -ItemType Directory -Path $outDir -Force }

[System.IO.File]::WriteAllText($Out, $json, (New-Object System.Text.UTF8Encoding $false))

$what = if ($sliceNote) { "$($entries.Count) keys ($sliceNote)" } else { "$($entries.Count) of $($neutral.Count) keys" }
Write-Host "Wrote $what for '$Culture' to:"
Write-Host "  $Out"
