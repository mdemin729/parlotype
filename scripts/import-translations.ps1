<#
.SYNOPSIS
    Builds Strings.<culture>.resx from a flat JSON translation map (ADR-069).

.DESCRIPTION
    The other half of the pipeline started by export-translation-brief.ps1. A
    translator returns { "key": "translation" } and this script owns every byte
    of XML that reaches the repo.

    The satellite file is produced by cloning the neutral Strings.resx and
    replacing only the <value> text, so key order, indentation and the resheader
    block are structurally identical across all 25 files by construction rather
    than by discipline.

    Comments are English developer notes, never UI copy, and are handled with one
    deliberate asymmetry: a comment the satellite already carries is kept, and
    the neutral file's is used only where the satellite has none. Round-tripping
    Strings.ru.resx is what forced this — several of its comments are
    language-specific guidance the neutral file cannot hold ("...so Russian uses
    the genitive here"), and one deliberately drops the neutral's reference to
    the "s" unit because Russian prints "с". Cloning the neutral comment
    unconditionally destroyed all of it. New languages, having no comments yet,
    still inherit the full neutral set.

    The script refuses to write on any of the four failures that a satellite
    resx can carry into production, so a bad translation never reaches the tree:

      1. a missing or orphan key
      2. a placeholder set that does not match the neutral value  (FormatException
         in front of a user, in a language nobody on the team reads)
      3. an empty or whitespace value
      4. a value over 25 characters that is byte-identical to the English, which
         is the signature of an unfinished pass

    Rule 4 is the same one LocalizationParityTests.NoKeyIsAccidentallyUntranslated
    enforces; catching it here just means the error names the key and the
    language on one line instead of failing a test run later.

.EXAMPLE
    pwsh scripts/import-translations.ps1 -Culture de -In de.json

.EXAMPLE
    pwsh scripts/import-translations.ps1 -Culture de -In de.partial.json -Merge
    # Applies only the supplied keys, keeping existing translations. The -Missing
    # half of a routine one-string change.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Culture,

    # Flat JSON object: { "Key_Name": "translation", ... }. Nothing else.
    [Parameter(Mandatory)]
    [string]$In,

    # Keep the translations already in Strings.<culture>.resx and apply only the
    # keys present in -In. Without it, -In must cover every key.
    [switch]$Merge
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resourcesDir = Join-Path $repoRoot 'src/Parlotype.Desktop/Resources'
$neutralResx = Join-Path $resourcesDir 'Strings.resx'
$targetResx = Join-Path $resourcesDir "Strings.$Culture.resx"

if ($Culture -eq 'en') {
    throw "'en' is Strings.resx itself. Edit it directly — it is the source, not a translation."
}
if (-not (Test-Path $In)) {
    throw "Translation map not found: $In"
}

function Get-Placeholders([string]$value) {
    return ([regex]::Matches($value, '\{(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value } |
        Sort-Object -Unique) -join ','
}

# ------------------------------------------------------------- read the inputs

$raw = Get-Content -Path $In -Raw -Encoding UTF8
try {
    $map = $raw | ConvertFrom-Json -AsHashtable
}
catch {
    throw "Could not parse $In as JSON: $($_.Exception.Message)"
}

# A translator that wrapped the map in the brief's own envelope is a common
# enough slip to be worth naming precisely rather than failing on 392 missing keys.
if ($map.ContainsKey('entries') -or $map.ContainsKey('culture')) {
    throw "$In looks like a brief, not a translation map. Expected a flat object: { `"Key`": `"translation`" }."
}

# PreserveWhitespace keeps the indentation text nodes intact, which is what makes
# the output byte-identical to a hand-authored file rather than reflowed.
$doc = New-Object System.Xml.XmlDocument
$doc.PreserveWhitespace = $true
$doc.Load($neutralResx)

# Whatever the target already holds, read whether or not -Merge was passed:
# -Merge decides what happens to existing *values*, but existing *comments* are
# preserved either way (see the note above).
$existing = @{}
$existingComments = @{}
if (Test-Path $targetResx) {
    $prior = New-Object System.Xml.XmlDocument
    $prior.PreserveWhitespace = $true
    $prior.Load($targetResx)
    foreach ($node in $prior.SelectNodes('/root/data')) {
        $name = $node.GetAttribute('name')
        $existing[$name] = $node.SelectSingleNode('value').InnerText
        $comment = $node.SelectSingleNode('comment')
        if ($comment) { $existingComments[$name] = $comment.InnerText }
    }
}
elseif ($Merge) {
    throw "-Merge needs an existing $targetResx."
}

# --------------------------------------------------------------- validate

$problems = [System.Collections.Generic.List[string]]::new()
$neutralKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

foreach ($node in $doc.SelectNodes('/root/data')) {
    $null = $neutralKeys.Add($node.GetAttribute('name'))
}

foreach ($key in $map.Keys) {
    if (-not $neutralKeys.Contains($key)) {
        $problems.Add("orphan key '$key' — not in Strings.resx")
    }
}

$applied = 0

foreach ($node in $doc.SelectNodes('/root/data')) {
    $key = $node.GetAttribute('name')
    $valueNode = $node.SelectSingleNode('value')
    $english = $valueNode.InnerText

    $translation = $null
    if ($map.ContainsKey($key)) {
        $translation = [string]$map[$key]
        $applied++
    }
    elseif ($Merge -and $existing.ContainsKey($key)) {
        $translation = $existing[$key]
    }
    else {
        $problems.Add("missing key '$key'")
        continue
    }

    if ([string]::IsNullOrWhiteSpace($translation)) {
        $problems.Add("key '$key' is empty")
        continue
    }

    $expected = Get-Placeholders $english
    $actual = Get-Placeholders $translation
    if ($expected -ne $actual) {
        $problems.Add("key '$key' has placeholders [$actual] but the English has [$expected]")
        continue
    }

    if ($english.Length -gt 25 -and $translation -ceq $english) {
        $problems.Add("key '$key' repeats the English verbatim — untranslated")
        continue
    }

    $valueNode.InnerText = $translation

    # Keep the satellite's own note where it has one; the neutral file's comment
    # (already present in this cloned node) stands in only where it does not.
    if ($existingComments.ContainsKey($key)) {
        $commentNode = $node.SelectSingleNode('comment')
        if ($commentNode) {
            $commentNode.InnerText = $existingComments[$key]
        }
        else {
            # The satellite carries a note for a key the neutral file does not
            # annotate. Rare, but real, and losing it silently would be the same
            # bug this whole branch exists to fix.
            $created = $doc.CreateElement('comment')
            $created.InnerText = $existingComments[$key]
            $null = $node.InsertAfter($created, $valueNode)
        }
    }
}

if ($problems.Count -gt 0) {
    $shown = $problems | Select-Object -First 25
    $more = if ($problems.Count -gt 25) { "`n  ... and $($problems.Count - 25) more" } else { '' }
    throw "Refusing to write Strings.$Culture.resx — $($problems.Count) problem(s):`n  " +
        ($shown -join "`n  ") + $more
}

# ------------------------------------------------------------------- write

# UTF-8 without BOM and CRLF, matching the existing files and the repo's
# core.autocrlf=true working copy.
#
# NewLineHandling must be Replace, not None. An XML parser normalizes CRLF to LF
# on load per the spec, so by the time the document is in memory every line break
# — the indentation between nodes and the two values that contain a real newline
# (Onboarding_Tray_Body, Settings_Data_DeleteDialog_BodyFormat) — is a bare LF.
# Writing that out verbatim produced a mixed-ending file that git flagged on the
# next add.
$settings = New-Object System.Xml.XmlWriterSettings
$settings.Encoding = New-Object System.Text.UTF8Encoding $false
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$settings.NewLineChars = "`r`n"
$settings.Indent = $false

$writer = [System.Xml.XmlWriter]::Create($targetResx, $settings)
try { $doc.Save($writer) } finally { $writer.Dispose() }

Write-Host "Wrote Strings.$Culture.resx — $applied key(s) applied, $($neutralKeys.Count) total."
