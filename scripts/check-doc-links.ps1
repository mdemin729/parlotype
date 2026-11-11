<#
.SYNOPSIS
    Fails when a tracked Markdown file links to something that does not exist.

.DESCRIPTION
    For every tracked *.md file (git ls-files):

      1. Relative links and images  — [text](target) and ![alt](target). External
         targets (http:, https:, mailto:, any scheme) and pure #anchors are ignored;
         a #fragment is stripped, the rest is URL-decoded and resolved against the
         file's directory. A target that does not exist is reported.
      2. Obsidian wikilinks         — any [[...]] outside code spans and fenced code
         blocks is reported (the memory/ vault is gone; nothing should resolve them).

    Links inside fenced code blocks and inline code spans are skipped. Output is
    `file:line -> target`, then a one-line summary. Exit code 1 if anything was
    reported, 0 otherwise.

.EXAMPLE
    pwsh scripts/check-doc-links.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
Set-Location $root

$files = git ls-files '*.md' | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }

$linkRx = [regex]'!?\[[^\]]*\]\(\s*(<[^>]*>|[^)\s]+)(?:\s+"[^"]*")?\s*\)'
$wikiRx = [regex]'\[\[[^\]]+\]\]'
$problems = New-Object System.Collections.Generic.List[string]
$linkCount = 0

foreach ($file in $files) {
    $dir = Split-Path -Parent (Join-Path $root $file)
    $inFence = $false
    $fenceMarker = ''
    $lineNo = 0
    foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $root $file))) {
        $lineNo++
        if ($inFence) {
            if ($line -match '^\s*(`{3,}|~{3,})\s*$' -and $Matches[1].StartsWith($fenceMarker)) { $inFence = $false }
            continue
        }
        if ($line -match '^\s*(`{3,}|~{3,})') {
            $inFence = $true
            $fenceMarker = $Matches[1].Substring(0, 3)
            continue
        }

        # Blank out inline code spans so links and wikilinks inside them are ignored.
        $text = [regex]::Replace($line, '(`+)(?:(?!\1).)+?\1', { param($m) ' ' * $m.Length })

        foreach ($m in $wikiRx.Matches($text)) {
            $problems.Add("${file}:${lineNo} -> $($m.Value) (Obsidian wikilink)")
        }

        foreach ($m in $linkRx.Matches($text)) {
            $target = $m.Groups[1].Value.Trim('<', '>')
            if ($target -match '^(?i:[a-z][a-z0-9+.-]*:)' -or $target.StartsWith('#') -or $target.StartsWith('//')) { continue }
            $linkCount++
            $path = $target -replace '[#?].*$', ''
            if ([string]::IsNullOrEmpty($path)) { continue }
            $path = [System.Uri]::UnescapeDataString($path)
            $full = if ($path.StartsWith('/')) { Join-Path $root $path.TrimStart('/') } else { Join-Path $dir $path }
            # Editor-style `file.cs:78` or `file.cs:78:5` suffixes point into an existing file.
            if (-not (Test-Path -LiteralPath $full)) { $full = $full -replace ':\d+(:\d+)?$', '' }
            if (-not (Test-Path -LiteralPath $full)) {
                $problems.Add("${file}:${lineNo} -> $target")
            }
        }
    }
}

$problems | ForEach-Object { Write-Host $_ }
Write-Host "$(@($files).Count) files, $linkCount links, $($problems.Count) broken"
if ($problems.Count -gt 0) { exit 1 }
exit 0
