<#
.SYNOPSIS
    Fails when a UI change leaves a translation stale (ADR-064, plan phase 2).

.DESCRIPTION
    Three checks:

      1. Key parity      — every key in the neutral Strings.resx exists in every
                           Strings.<culture>.resx named by SupportedUiLanguages,
                           and no locale carries keys the neutral file has lost.
      2. Placeholder     — the {0}/{1} set matches per key across locales. A
         parity            translation that drops a placeholder throws
                           FormatException at runtime, in production, in the one
                           language nobody on the team reads.
      3. Hardcoded text  — no new literal UI copy in .axaml. Files that predate
                           the localization work are recorded in
                           localization-baseline.json with their literal count;
                           the count may shrink (run -UpdateBaseline) but never
                           grow, and a file that reaches zero must leave the list.

    Checks 1 and 2 are also xUnit facts (LocalizationParityTests) so CI and
    `dotnet test` enforce them too — this script exists to give a Claude Code
    hook something fast and specific to report.

.EXAMPLE
    pwsh scripts/check-localization.ps1

.EXAMPLE
    pwsh scripts/check-localization.ps1 -UpdateBaseline
    # After migrating a view's copy into resx, to record the new (lower) counts.
#>
[CmdletBinding()]
param(
    # Rewrite localization-baseline.json from the current tree. Only ever used to
    # record progress: the script still refuses to raise a count.
    [switch]$UpdateBaseline,

    # Rewrite the baseline even where counts went UP. The only legitimate reason
    # is that this script's own attribute list got wider and is now seeing copy it
    # used to miss — never a code change that added hardcoded text.
    #
    # It is deliberately a separate switch so the git diff shows the scanner
    # change and the baseline increase landing together, where a reviewer can see
    # that one explains the other. If you reach for this without having just
    # edited $attributes above, you are using it wrong.
    [switch]$Rescan
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resourcesDir = Join-Path $repoRoot 'src/Parlotype.Desktop/Resources'
$neutralResx = Join-Path $resourcesDir 'Strings.resx'
$registryPath = Join-Path $repoRoot 'src/Parlotype.Core/Localization/SupportedUiLanguages.cs'
$baselinePath = Join-Path $repoRoot 'scripts/localization-baseline.json'
$axamlRoot = Join-Path $repoRoot 'src/Parlotype.Desktop'

$problems = [System.Collections.Generic.List[string]]::new()

function Read-ResxEntries([string]$path) {
    [xml]$doc = Get-Content -Path $path -Raw -Encoding UTF8
    $map = [ordered]@{}
    foreach ($entry in $doc.root.data) {
        if ($null -ne $entry.name) { $map[$entry.name] = [string]$entry.value }
    }
    return $map
}

function Get-Placeholders([string]$value) {
    return ([regex]::Matches($value, '\{(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value } |
        Sort-Object -Unique) -join ','
}

# ---------------------------------------------------------------- 1 + 2. Resx

$neutral = Read-ResxEntries $neutralResx

# The registry in Core is the source of truth for which languages must exist.
# Regex rather than a parse: the file is ours and the shape is fixed.
$registry = Get-Content -Path $registryPath -Raw -Encoding UTF8
$cultures = [regex]::Matches($registry, 'new\("(?<culture>[a-z]{2}(?:-[A-Za-z]+)?)"\s*,') |
    ForEach-Object { $_.Groups['culture'].Value } |
    Where-Object { $_ -ne 'en' }

if ($cultures.Count -eq 0) {
    $problems.Add("No translated languages found in SupportedUiLanguages.cs — the registry regex may have gone stale.")
}

foreach ($culture in $cultures) {
    $path = Join-Path $resourcesDir "Strings.$culture.resx"
    if (-not (Test-Path $path)) {
        $problems.Add("$culture is in SupportedUiLanguages but Strings.$culture.resx does not exist.")
        continue
    }

    $translated = Read-ResxEntries $path

    foreach ($key in $neutral.Keys) {
        if (-not $translated.Contains($key)) {
            $problems.Add("Strings.$culture.resx is missing key '$key'.")
            continue
        }

        $expected = Get-Placeholders $neutral[$key]
        $actual = Get-Placeholders $translated[$key]
        if ($expected -ne $actual) {
            $problems.Add("Strings.$culture.resx key '$key' has placeholders [$actual] but the neutral file has [$expected].")
        }
    }

    foreach ($key in $translated.Keys) {
        if (-not $neutral.Contains($key)) {
            $problems.Add("Strings.$culture.resx has orphan key '$key' (not in Strings.resx).")
        }
    }
}

# ------------------------------------------------- 3. {loc:Tr} keys that exist

# A typo in {loc:Tr Setings_Theme_Title} neither fails the build nor throws — the
# window just renders the raw key. Nothing else catches it.
$trPattern = [regex]'\{\s*loc:Tr\s+([A-Za-z0-9_]+)\s*\}'
foreach ($file in Get-ChildItem -Path $axamlRoot -Filter *.axaml -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($repoRoot.Length + 1) -replace '\\', '/'
    foreach ($match in $trPattern.Matches((Get-Content -Path $file.FullName -Raw -Encoding UTF8))) {
        $key = $match.Groups[1].Value
        if (-not $neutral.Contains($key)) {
            $problems.Add("$relative uses {loc:Tr $key} but that key is not in Strings.resx.")
        }
    }
}

# ------------------------------------------------------- 4. Hardcoded literals

# Attributes that put words on screen. PlaceholderText and ToolTipText are here
# because they are easy to forget and just as visible as Text.
$attributes = 'Text', 'Content', 'OnContent', 'OffContent', 'Header', 'Watermark',
              'PlaceholderText', 'ToolTip\.Tip', 'ToolTipText', 'Title'
$literalPattern = [regex]("\b(" + ($attributes -join '|') + ")\s*=\s*""([^""]*)""")

# Elements that render their own inner text.
$contentPattern = [regex]'(?s)<(TextBlock|Button|Run|TextBox|RadioButton|CheckBox|Expander)\b[^>]*?>(?!\s*<)(.*?)</\1>'

$baseline = if (Test-Path $baselinePath) {
    Get-Content -Path $baselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    [pscustomobject]@{ allowedValues = @(); pendingFiles = [pscustomobject]@{} }
}

$allowedValues = @($baseline.allowedValues)
$pending = @{}
foreach ($property in $baseline.pendingFiles.PSObject.Properties) {
    $pending[$property.Name] = [int]$property.Value
}

$counts = [ordered]@{}
foreach ($file in Get-ChildItem -Path $axamlRoot -Filter *.axaml -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($repoRoot.Length + 1) -replace '\\', '/'
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8

    $hits = 0
    foreach ($match in $literalPattern.Matches($content)) {
        $value = $match.Groups[2].Value.Trim()
        if ([string]::IsNullOrEmpty($value)) { continue }
        if ($value.StartsWith('{')) { continue }              # a binding, incl. {loc:Tr}
        # Numeric character references are glyphs, but the 'x' in &#x2715; would
        # otherwise read as a letter and count the glyph as translatable copy.
        $bare = [regex]::Replace($value, '&#x?[0-9A-Fa-f]+;', '')
        if ($bare -notmatch '[A-Za-zЀ-ӿ]') { continue }   # glyphs: ✕ → ✓ ●
        if ($allowedValues -contains $value) { continue }     # brand name, identifiers
        $hits++
    }

    # Copy written as element *content* rather than an attribute —
    # <TextBlock>some words</TextBlock>. The attribute scan above cannot see it,
    # which is how two untranslated paragraphs survived a whole extraction pass
    # and turned up only in a rendered screenshot.
    foreach ($match in $contentPattern.Matches($content)) {
        $body = $match.Groups[2].Value.Trim()
        if ([string]::IsNullOrEmpty($body)) { continue }
        if ($body.StartsWith('<') -or $body.StartsWith('{')) { continue }
        $bare = [regex]::Replace($body, '&#x?[0-9A-Fa-f]+;', '')
        if ($bare -notmatch '[A-Za-zЀ-ӿ]') { continue }
        if ($allowedValues -contains $body) { continue }
        $hits++
    }

    if ($hits -gt 0) { $counts[$relative] = $hits }
}

if ($UpdateBaseline) {
    foreach ($relative in $counts.Keys) {
        if (-not $Rescan -and $pending.ContainsKey($relative) -and $counts[$relative] -gt $pending[$relative]) {
            $problems.Add("$relative has $($counts[$relative]) hardcoded literals, up from $($pending[$relative]). -UpdateBaseline records progress; it does not bless regressions. (If the scanner's attribute list just got wider, use -Rescan.)")
        }
    }
} else {
    foreach ($relative in $counts.Keys) {
        if (-not $pending.ContainsKey($relative)) {
            $problems.Add("$relative has $($counts[$relative]) hardcoded UI literal(s). Move the copy into Strings.resx and bind it with {loc:Tr Key}.")
        }
        elseif ($counts[$relative] -gt $pending[$relative]) {
            $problems.Add("$relative has $($counts[$relative]) hardcoded UI literal(s), up from $($pending[$relative]). New copy must go through Strings.resx and {loc:Tr Key}.")
        }
    }

    foreach ($relative in $pending.Keys) {
        $now = if ($counts.Contains($relative)) { $counts[$relative] } else { 0 }
        if ($now -lt $pending[$relative]) {
            $problems.Add("$relative is down to $now hardcoded literal(s) from $($pending[$relative]) — good. Run: pwsh scripts/check-localization.ps1 -UpdateBaseline")
        }
    }
}

# ------------------------------------------------------------------- Reporting

if (($UpdateBaseline -or $Rescan) -and $problems.Count -eq 0) {
    $ordered = [ordered]@{}
    foreach ($relative in ($counts.Keys | Sort-Object)) { $ordered[$relative] = $counts[$relative] }

    $updated = [ordered]@{
        '$comment'    = 'Hardcoded UI copy still awaiting extraction (ADR-064, localization plan phase 3). Counts may shrink, never grow. Regenerate with: pwsh scripts/check-localization.ps1 -UpdateBaseline'
        allowedValues = $allowedValues
        pendingFiles  = $ordered
    }

    $json = ($updated | ConvertTo-Json -Depth 5) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($baselinePath, $json + "`n", [System.Text.UTF8Encoding]::new($false))

    $remaining = ($counts.Values | Measure-Object -Sum).Sum
    Write-Host "Baseline updated: $($ordered.Count) file(s), $remaining literal(s) still to extract."
    exit 0
}

if ($problems.Count -gt 0) {
    Write-Host "Localization check failed:`n" -ForegroundColor Red
    foreach ($problem in $problems) { Write-Host "  - $problem" }
    Write-Host ''
    exit 1
}

$remaining = ($counts.Values | Measure-Object -Sum).Sum
$keyCount = $neutral.Count
$languages = ($cultures -join ', ')
Write-Host "Localization OK: $keyCount keys x [en, $languages]; $remaining literal(s) still awaiting extraction."
exit 0
