<#
.SYNOPSIS
    Validating and comparing .NET composite format strings (ADR-069).

.DESCRIPTION
    Dot-sourced by export-translation-brief.ps1, import-translations.ps1,
    gen-strings.ps1 and check-localization.ps1 so all four agree on what a
    placeholder is. They used to share only a regex, `\{(\d+)`, and that regex
    cannot see three separate ways to break a format string:

      "Schritt {0x} von {1}"    regex says [0,1] — string.Format THROWS
      "Schritt {0 von {1}"      regex says [0,1] — string.Format THROWS
      "Schritt {{0}} von {1}"   regex says [0,1] — {{0}} is a LITERAL "{0}",
                                so argument 0 is silently dropped

    All three pass a set-equality check against an English "{0} … {1}" and reach
    production, where the first two throw FormatException in a language nobody on
    the team reads and the third quietly loses a value.

    So neither function parses braces by hand. Validity is decided by calling
    string.Format itself — the same parser that runs in production — and the
    signature is derived from which arguments actually survive substitution,
    which is the only definition that treats {{0}} correctly.
#>

# No Set-StrictMode here: this file is dot-sourced, so anything it sets applies
# to the *caller's* scope and would change how the importing script behaves.

<#
.SYNOPSIS
    Hides named tokens from the composite-format parser.

.DESCRIPTION
    Not every brace in this app's copy is a String.Format item.
    Settings_Prompts_Help_BuiltInBody documents the Gemma prompt syntax and
    contains {speech_lang} and {text_lang}; that string is never passed to
    String.Format at all, and the prompt engine substitutes those by name.

    Without this, validation flagged all 25 languages of that key at once — a
    false positive found by running the new check over the real tree rather than
    over test cases. The pattern deliberately requires a leading letter or
    underscore, so "{0x}" is NOT masked and still fails validation as it should.
#>
function Hide-NamedTokens([string]$value) {
    return [regex]::Replace($value, '\{([A-Za-z_]\w*)\}', '$1')
}

# Arbitrarily above anything this app uses (the widest key takes three). A
# larger index is far more likely to be a typo than a real intent, and without a
# ceiling one would allocate an argument array of that size.
$script:MaxFormatIndex = 15

<#
.SYNOPSIS
    $null when the value is a well-formed composite format string; otherwise a
    one-line reason.
#>
function Test-CompositeFormat([string]$value) {
    if ($null -eq $value) { return 'value is null' }

    $value = Hide-NamedTokens $value
    $indices = [regex]::Matches($value, '\{(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value }

    $highest = if ($indices) { ($indices | Measure-Object -Maximum).Maximum } else { -1 }
    if ($highest -gt $script:MaxFormatIndex) {
        return "uses {$highest}, above the supported maximum of {$script:MaxFormatIndex}"
    }

    # A bare "{" or "}" that is not part of a well-formed item makes Format throw,
    # which is exactly the answer wanted.
    $args = @(0..$script:MaxFormatIndex | ForEach-Object { "x" })
    try {
        $null = [string]::Format($value, $args)
        return $null
    }
    catch [FormatException] {
        return "is not a valid composite format string: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    The ordered set of argument indices this format actually substitutes, as a
    comparable string. Returns $null when the value is not a valid format.

.DESCRIPTION
    Formats with sentinel arguments and reports which sentinels survive. An
    escaped "{{0}}" renders as the literal text "{0}" and contributes no
    sentinel, so it is correctly reported as substituting nothing — the case a
    regex over braces gets wrong.
#>
function Get-FormatSignature([string]$value) {
    if (Test-CompositeFormat $value) { return $null }

    $value = Hide-NamedTokens $value
    $sentinels = @(0..$script:MaxFormatIndex | ForEach-Object { "`u{0001}$_`u{0001}" })
    $rendered = [string]::Format($value, $sentinels)

    $used = 0..$script:MaxFormatIndex | Where-Object { $rendered.Contains($sentinels[$_]) }
    return ($used | Sort-Object) -join ','
}
