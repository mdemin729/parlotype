<#
.SYNOPSIS
    Regenerates docs/decisions/README.md and docs/knowledge/README.md from frontmatter.

.DESCRIPTION
    Both READMEs are indexes, never hand-written: each row is read from the YAML
    frontmatter of the note it points at, so the index cannot drift from the notes.

    docs/decisions/README.md
        One row per ADR (NNN-*.md): number, title (the H1 with its "NNN." prefix
        removed), status, and its relations (supersedes / superseded-by / amends /
        amended-by). The script also fails -Check when a relation is one-sided
        (A amends B, but B does not list A under amended-by), because a link that
        only works in one direction is the usual way ADR metadata rots.

    docs/knowledge/README.md
        One table per `area`; each row is a note linked by its `title` with its
        `summary`. Notes carry the frontmatter keys title, area, summary, verified.
        When docs/knowledge/ does not exist this part is skipped silently.

    Frontmatter is read with plain string handling (flat `key: value` lines, inline
    `[a, b]` lists, optional quotes) - no YAML module is needed. Output is
    deterministic and LF-terminated, so running the script twice changes nothing.

.EXAMPLE
    pwsh scripts/gen-docs-index.ps1                 # rewrite whichever indexes are stale
    pwsh scripts/gen-docs-index.ps1 -Check          # CI / pre-commit: exit 1 if stale
    pwsh scripts/gen-docs-index.ps1 -Only adr       # touch only docs/decisions/README.md
#>
[CmdletBinding()]
param(
    # Verify only: exit 1 if a generated file is stale or an ADR relation is one-sided,
    # and write nothing.
    [switch]$Check,

    # Restrict the run to one index. Default regenerates both.
    [ValidateSet('all', 'adr', 'knowledge')]
    [string]$Only = 'all'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$decisionsDir = Join-Path $repoRoot 'docs/decisions'
$knowledgeDir = Join-Path $repoRoot 'docs/knowledge'

function Read-Frontmatter {
    # Returns an ordered hashtable of the top-of-file `---` block, or $null when absent.
    param([string]$Path)

    $lines = (Get-Content -Path $Path -Raw -Encoding UTF8) -split "`r?`n"
    if ($lines.Count -lt 2 -or $lines[0].Trim() -ne '---') { return $null }

    $meta = [ordered]@{}
    $lastKey = $null
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.Trim() -eq '---') { return $meta }
        if ($line -match '^\s*#') { continue }
        if ($line -match '^([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$') {
            $lastKey = $Matches[1]
            $meta[$lastKey] = $Matches[2].Trim()
        }
        elseif ($lastKey -and $line -match '^\s+\S') {
            # Continuation of a folded / multi-line scalar.
            $meta[$lastKey] = ("$($meta[$lastKey]) $($line.Trim())").Trim()
        }
    }
    return $null   # no closing fence
}

function ConvertTo-Scalar {
    param([string]$Value)
    $v = $Value.Trim()
    if ($v -match '^>[-+]?$|^\|[-+]?$') { return '' }
    if ($v.Length -ge 2 -and $v.StartsWith('"') -and $v.EndsWith('"')) {
        return $v.Substring(1, $v.Length - 2).Replace('\"', '"').Replace('\\', '\')
    }
    if ($v.Length -ge 2 -and $v.StartsWith("'") -and $v.EndsWith("'")) {
        return $v.Substring(1, $v.Length - 2).Replace("''", "'")
    }
    return $v
}

function ConvertTo-List {
    # "[049, 053]" or "049" -> @('049','053'); empty -> @().
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    $v = $Value.Trim().TrimStart('[').TrimEnd(']')
    return @($v -split ',' | ForEach-Object { $_.Trim().Trim('"', "'") } | Where-Object { $_ })
}

function Format-Cell {
    # Table cells are single-line and must not contain a raw pipe.
    param([string]$Text)
    return ($Text -replace '\s+', ' ').Replace('|', '\|').Trim()
}

function Test-SameContent {
    param([string]$Path, [string]$Expected)
    if (-not (Test-Path $Path)) { return $false }
    $current = (Get-Content -Path $Path -Raw -Encoding UTF8) -replace "`r`n", "`n"
    return $current -ceq $Expected
}

function Write-Generated {
    param([string]$Path, [string]$Content, [string]$Label)
    if (Test-SameContent -Path $Path -Expected $Content) {
        Write-Host "up to date: $Label"
        return $true
    }
    if ($Check) {
        Write-Host "STALE: $Label - run 'pwsh scripts/gen-docs-index.ps1'" -ForegroundColor Red
        return $false
    }
    [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "wrote: $Label"
    return $true
}

# ---------------------------------------------------------------- ADR index

function Build-AdrIndex {
    $adrs = @()
    foreach ($file in (Get-ChildItem -Path $decisionsDir -Filter '*.md' |
            Where-Object { $_.Name -match '^(\d{3})-.+\.md$' } | Sort-Object Name)) {
        $number = $file.Name.Substring(0, 3)
        $meta = Read-Frontmatter -Path $file.FullName
        if ($null -eq $meta) { throw "$($file.Name): missing frontmatter" }

        $h1 = (Get-Content -Path $file.FullName -Encoding UTF8 | Where-Object { $_ -match '^# ' } | Select-Object -First 1)
        if (-not $h1) { throw "$($file.Name): no H1 title" }
        $title = ($h1 -replace '^#\s+', '') -replace '^(ADR[- ]?)?\d{3}[.:]\s*', ''

        $status = ConvertTo-Scalar $meta['status']
        if (-not $status) { throw "$($file.Name): frontmatter has no status" }

        $adrs += [pscustomobject]@{
            Number       = $number
            File         = $file.Name
            Title        = $title
            Status       = $status
            Supersedes   = ConvertTo-List $meta['supersedes']
            SupersededBy = ConvertTo-List $meta['superseded-by']
            Amends       = ConvertTo-List $meta['amends']
            AmendedBy    = ConvertTo-List $meta['amended-by']
        }
    }

    # Every relation must exist in both directions.
    $byNumber = @{}
    foreach ($a in $adrs) { $byNumber[$a.Number] = $a }
    $problems = @()
    foreach ($a in $adrs) {
        foreach ($t in $a.Supersedes) {
            if (-not $byNumber.ContainsKey($t)) { $problems += "$($a.Number) supersedes unknown ADR $t"; continue }
            if ($byNumber[$t].SupersededBy -notcontains $a.Number) { $problems += "$($a.Number) supersedes $t, but $t has no superseded-by: [$($a.Number)]" }
            if ($byNumber[$t].Status -ne 'superseded') { $problems += "$t is superseded by $($a.Number) but its status is '$($byNumber[$t].Status)'" }
        }
        foreach ($t in $a.SupersededBy) {
            if (-not $byNumber.ContainsKey($t)) { $problems += "$($a.Number) superseded-by unknown ADR $t"; continue }
            if ($byNumber[$t].Supersedes -notcontains $a.Number) { $problems += "$($a.Number) is superseded-by $t, but $t has no supersedes: [$($a.Number)]" }
        }
        foreach ($t in $a.Amends) {
            if (-not $byNumber.ContainsKey($t)) { $problems += "$($a.Number) amends unknown ADR $t"; continue }
            if ($byNumber[$t].AmendedBy -notcontains $a.Number) { $problems += "$($a.Number) amends $t, but $t has no amended-by: [$($a.Number)]" }
        }
        foreach ($t in $a.AmendedBy) {
            if (-not $byNumber.ContainsKey($t)) { $problems += "$($a.Number) amended-by unknown ADR $t"; continue }
            if ($byNumber[$t].Amends -notcontains $a.Number) { $problems += "$($a.Number) is amended-by $t, but $t has no amends: [$($a.Number)]" }
        }
    }
    foreach ($group in ($adrs | Group-Object Number | Where-Object Count -gt 1)) {
        $problems += "duplicate ADR number $($group.Name)"
    }

    $link = {
        param([string[]]$Numbers)
        ($Numbers | ForEach-Object {
            if ($byNumber.ContainsKey($_)) { "[$_]($($byNumber[$_].File))" } else { $_ }
        }) -join ', '
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append("# Architecture Decision Records`n`n")
    [void]$sb.Append("An ADR records one decision: the context that forced it, what was decided, and what it`n")
    [void]$sb.Append("costs. The body of an accepted ADR is a historical record and is not rewritten; when a`n")
    [void]$sb.Append("decision changes, a later ADR says so. What is kept current is the frontmatter (``status```n")
    [void]$sb.Append("and the ``supersedes`` / ``superseded-by`` / ``amends`` / ``amended-by`` links), and that is`n")
    [void]$sb.Append("what this table is built from. New ADRs start from [``_template.md``](_template.md).`n`n")
    [void]$sb.Append("This file is generated by ``scripts/gen-docs-index.ps1`` - do not edit it by hand.`n")
    [void]$sb.Append("``pwsh scripts/gen-docs-index.ps1 -Check`` fails when it is stale.`n`n")
    [void]$sb.Append("| Number | Title | Status | Superseded by / Amends |`n")
    [void]$sb.Append("|---|---|---|---|`n")
    foreach ($a in $adrs) {
        $rel = @()
        if ($a.SupersededBy.Count) { $rel += 'Superseded by ' + (& $link $a.SupersededBy) }
        if ($a.Supersedes.Count) { $rel += 'Supersedes ' + (& $link $a.Supersedes) }
        if ($a.Amends.Count) { $rel += 'Amends ' + (& $link $a.Amends) }
        if ($a.AmendedBy.Count) { $rel += 'Amended by ' + (& $link $a.AmendedBy) }
        $cell = ($rel -join '; ')
        [void]$sb.Append("| $($a.Number) | [$(Format-Cell $a.Title)]($($a.File)) | $($a.Status) | $cell |`n")
    }

    return [pscustomobject]@{ Content = $sb.ToString(); Problems = $problems }
}

# ---------------------------------------------------------- knowledge index

function Build-KnowledgeIndex {
    $notes = @()
    foreach ($file in (Get-ChildItem -Path $knowledgeDir -Filter '*.md' -Recurse |
            Where-Object { $_.Name -ne 'README.md' } | Sort-Object FullName)) {
        $meta = Read-Frontmatter -Path $file.FullName
        $rel = $file.FullName.Substring($knowledgeDir.Length).TrimStart('\', '/').Replace('\', '/')
        if ($null -eq $meta) { throw "docs/knowledge/${rel}: missing frontmatter" }
        foreach ($key in 'title', 'area', 'summary') {
            if (-not $meta.Contains($key) -or -not (ConvertTo-Scalar $meta[$key])) {
                throw "docs/knowledge/${rel}: frontmatter has no '$key'"
            }
        }
        $notes += [pscustomobject]@{
            Path    = $rel
            Title   = ConvertTo-Scalar $meta['title']
            Area    = ConvertTo-Scalar $meta['area']
            Summary = ConvertTo-Scalar $meta['summary']
        }
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append("# Knowledge notes`n`n")
    [void]$sb.Append("Facts that cannot be derived from the code: third-party quirks, environment gotchas, and`n")
    [void]$sb.Append("measured limits. Anything an ADR or the code already states does not belong here; link`n")
    [void]$sb.Append("to the ADR instead. Each note carries ``title``, ``area``, ``summary`` and ``verified`` in its`n")
    [void]$sb.Append("frontmatter.`n`n")
    [void]$sb.Append("This file is generated by ``scripts/gen-docs-index.ps1`` - do not edit it by hand.`n")
    [void]$sb.Append("``pwsh scripts/gen-docs-index.ps1 -Check`` fails when it is stale.`n")

    foreach ($group in ($notes | Group-Object Area | Sort-Object { $_.Name.ToLowerInvariant() }, Name)) {
        [void]$sb.Append("`n## $($group.Name)`n`n")
        [void]$sb.Append("| Note | Summary |`n|---|---|`n")
        foreach ($n in ($group.Group | Sort-Object { $_.Title.ToLowerInvariant() }, Path)) {
            $url = $n.Path -replace ' ', '%20'
            [void]$sb.Append("| [$(Format-Cell $n.Title)]($url) | $(Format-Cell $n.Summary) |`n")
        }
    }
    return $sb.ToString()
}

# --------------------------------------------------------------------- main

$ok = $true

if ($Only -in 'all', 'adr') {
    $adr = Build-AdrIndex
    foreach ($p in $adr.Problems) {
        Write-Host "ADR metadata: $p" -ForegroundColor $(if ($Check) { 'Red' } else { 'Yellow' })
    }
    if ($Check -and $adr.Problems.Count) { $ok = $false }
    if (-not (Write-Generated -Path (Join-Path $decisionsDir 'README.md') -Content $adr.Content -Label 'docs/decisions/README.md')) { $ok = $false }
}

if ($Only -in 'all', 'knowledge') {
    if (Test-Path $knowledgeDir) {
        $content = Build-KnowledgeIndex
        if (-not (Write-Generated -Path (Join-Path $knowledgeDir 'README.md') -Content $content -Label 'docs/knowledge/README.md')) { $ok = $false }
    }
    else {
        Write-Host 'skipped: docs/knowledge/ does not exist yet'
    }
}

if (-not $ok) { exit 1 }
