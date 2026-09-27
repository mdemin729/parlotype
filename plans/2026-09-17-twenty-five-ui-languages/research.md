---
title: Research — culture codes, endonyms and locale behaviour for 22 new UI languages
status: reference
created: 2026-09-17
---

## 1. The language table

All 22 codes were verified with a throwaway console app (`net10.0`, `InvariantGlobalization=false`) calling `CultureInfo.GetCultureInfo(code)` and printing `.Name`, `.NativeName`, `.EnglishName`, `.IsNeutralCulture`, and `.TextInfo.IsRightToLeft`. Every code below returned `IsNeutralCulture=True` and `IsRightToLeft=False` — no guessing, no RTL surprises.

| English name | .NET neutral culture name | Endonym (for `SupportedUiLanguages.All`) | Script | Notes |
|---|---|---|---|---|
| Bulgarian | `bg` | **Български** | Cyrillic | `.NativeName` returns lowercase `български`. Capitalized to match the existing `Русский`/`Español` list convention (capitalize the first letter regardless of the language's own sentence-case habits — this is how Facebook/Google-style language pickers render endonyms as list labels, not as running prose). |
| Croatian | `hr` | **Hrvatski** | Latin | `.NativeName` = `hrvatski` (lowercase). Capitalized per the list convention above. |
| Czech | `cs` | **Čeština** | Latin | `.NativeName` = `čeština` (lowercase). Capitalized. |
| Danish | `da` | **Dansk** | Latin | `.NativeName` = `dansk` (lowercase). Capitalized. |
| Dutch | `nl` | **Nederlands** | Latin | `.NativeName` = `Nederlands` — .NET already capitalizes it (Dutch capitalizes the demonym/language-noun form). Use as-is. |
| Estonian | `et` | **Eesti** | Latin | `.NativeName` = `eesti` (lowercase). Capitalized. |
| Finnish | `fi` | **Suomi** | Latin | `.NativeName` = `suomi` (lowercase). Capitalized. |
| French | `fr` | **Français** | Latin | `.NativeName` = `français` (lowercase). Capitalized. |
| German | `de` | **Deutsch** | Latin | `.NativeName` = `Deutsch` — already capitalized (German capitalizes all nouns). Use as-is. |
| Greek | `el` | **Ελληνικά** | Greek | `.NativeName` = `Ελληνικά` — already capitalized (Greek capitalizes the initial letter as a proper/list form). Use as-is. |
| Hungarian | `hu` | **Magyar** | Latin | `.NativeName` = `magyar` (lowercase). Capitalized. |
| Italian | `it` | **Italiano** | Latin | `.NativeName` = `italiano` (lowercase). Capitalized. |
| Latvian | `lv` | **Latviešu** | Latin | `.NativeName` = `latviešu` (lowercase). Capitalized. |
| Lithuanian | `lt` | **Lietuvių** | Latin | `.NativeName` = `lietuvių` (lowercase). Capitalized. |
| Maltese | `mt` | **Malti** | Latin | `.NativeName` = `Malti` — already capitalized. Use as-is. |
| Polish | `pl` | **Polski** | Latin | `.NativeName` = `polski` (lowercase). Capitalized. |
| Portuguese | `pt` | **Português** | Latin | `.NativeName` = `português` (lowercase). Capitalized. See §2 for the `pt` vs `pt-PT`/`pt-BR` decision. |
| Romanian | `ro` | **Română** | Latin | `.NativeName` = `română` (lowercase). Capitalized. |
| Slovak | `sk` | **Slovenčina** | Latin | `.NativeName` = `slovenčina` (lowercase). Capitalized. |
| Slovenian | `sl` | **Slovenščina** | Latin | `.NativeName` = `slovenščina` (lowercase). Capitalized. |
| Swedish | `sv` | **Svenska** | Latin | `.NativeName` = `svenska` (lowercase). Capitalized. |
| Ukrainian | `uk` | **Українська** | Cyrillic | `.NativeName` = `українська` (lowercase). Capitalized. |

**Rule applied:** capitalize the first letter of every endonym, to match the existing rows (`Русский`, `Español`, `English`) — that is the convention this codebase already committed to, independent of whether the language's own orthography lowercases its name in running text. Six of the 22 (`nl`, `de`, `el`, `mt`, plus `en`/`es`/`ru` already shipped) happen to already be capitalized by .NET's `NativeName`; the other 16 need the first letter capitalized relative to what `CultureInfo.NativeName` returns.

**Script distribution across all 25 shipped languages:** Latin — `en`, `es`, `hr`, `cs`, `da`, `nl`, `et`, `fi`, `fr`, `de`, `hu`, `it`, `lv`, `lt`, `mt`, `pl`, `pt`, `ro`, `sk`, `sl`, `sv` (21 of 25). Cyrillic — `ru`, `bg`, `uk` (3 of 25). Greek — `el` (1 of 25).

**RTL:** confirmed via `TextInfo.IsRightToLeft` — every one of the 22 new codes returned `False`. None of the 25 Parakeet languages are right-to-left scripts (no Arabic, Hebrew, Persian, etc. in the set), so no RTL layout work is triggered by this change.

## 2. Portuguese and other variant questions

Verified via the same probe, also requesting `pt-PT` and `pt-BR` explicitly:

```
pt      .Name=pt     .NativeName=português           .EnglishName=Portuguese            .IsNeutralCulture=True
pt-PT   .Name=pt-PT  .NativeName=português (Portugal) .EnglishName=Portuguese (Portugal) .IsNeutralCulture=False
pt-BR   .Name=pt-BR  .NativeName=português (Brasil)   .EnglishName=Portuguese (Brazil)   .IsNeutralCulture=False
```

**.NET satellite resource fallback** walks the culture's parent chain, not sideways or downward: `ResourceManager` for a `pt-BR` UI culture looks for a `Strings.pt-BR.resources` satellite first; if that assembly does not exist, it falls back to the *parent* neutral culture, `pt`, and uses `Strings.pt.resources` if that exists; only then does it fall back to the neutral/main assembly (English). The reverse is not true: a `pt` (or `pt-PT`) request does **not** fall back to a `pt-BR` satellite, because `pt-BR` is a child of `pt`, not a parent — a satellite shipped only as `pt-BR` would leave a Portugal (`pt-PT`) user on English.

**Recommendation:** ship a single neutral `Strings.pt.resx`, exactly as Parakeet lists plain "Portuguese" and exactly as the existing three languages (`en`, `ru`, `es`) are all neutral, two-letter codes. This is also consistent with `SupportedUiLanguages.MatchSystemCulture`, which already matches on the neutral prefix of any system culture (a `pt-BR` or `pt-PT` Windows install both resolve to the `pt` row via the `neutral = systemCultureName[..separator]` fallback already in `Find(systemCultureName) ?? Find(neutral)`). No code change is needed in `MatchSystemCulture` — it already does the right thing for any language with regional variants, as long as the shipped row uses the neutral code.

**Other variant splits checked:** French (`fr-FR`/`fr-CA`) and German (`de-DE`/`de-AT`/`de-CH`) are the only other languages in the list with real regional culture codes in .NET, but Parakeet lists them as plain "French"/"German" (no variant split), and the same neutral-culture argument applies identically — ship `fr` and `de` as neutral, and any regional Windows install falls back to them via the same `MatchSystemCulture` neutral-prefix match. No other language in the 22 has a meaningful UI-relevant variant split (Bulgarian, Croatian, Czech, Danish, Dutch, Estonian, Finnish, Greek, Hungarian, Italian, Latvian, Lithuanian, Maltese, Polish, Romanian, Slovak, Slovenian, Swedish, Ukrainian are all effectively single-country/single-standard languages for UI purposes). Conclusion: use the plain two-letter neutral code for all 22, with no exceptions.

## 3. Satellite assembly and packaging impact

**Files read:** `src/Parlotype.Desktop/Parlotype.Desktop.csproj`, `Directory.Build.props`, `Directory.Build.targets` (repo root).

- `Parlotype.Desktop.csproj` has no `SatelliteResourceLanguages` property, and a repo-wide `grep -rn "SatelliteResourceLanguages"` across every `.props`/`.targets`/`.csproj` in the repo returned **no matches**. This property, when set, restricts which satellite cultures the SDK packs into the build/publish output — since it is absent, the default MSBuild/Roslyn behavior applies: **every** `Strings.<culture>.resx` that exists gets its own satellite assembly, with no allow-list to update when adding languages.
- `Directory.Build.targets` contains three publish-time filtering targets, all read in full:
  1. `RemoveUnusedOnnxRuntimeProvidersFromBuild`/`FromPublish` (ADR-050) — matches on `%(Filename)` containing `onnxruntime_providers_cuda` or `onnxruntime_providers_tensorrt`. Cannot match a culture-named directory or a `Parlotype.resources.dll` filename.
  2. `RemoveForeignRuntimeAssetsFromPublish` (ADR-051) — matches when the **immediate parent directory name** of a resolved publish file is in the hard-coded RID list `linux-arm;linux-arm64;linux-x64;macos-arm64;macos-x64;win-arm64;win-x64;win-x86`. None of the 22 new culture codes (`bg`, `hr`, `cs`, `da`, `nl`, `et`, `fi`, `fr`, `de`, `el`, `hu`, `it`, `lv`, `lt`, `mt`, `pl`, `pt`, `ro`, `sk`, `sl`, `sv`, `uk`) collide with any entry in that RID list, so satellite directories are never caught by this filter. (Worth flagging for later, not urgent: if a Whisper.net runtime package is ever built for a RID abbreviated the same as a culture code, this could silently start eating a locale — today there is no collision.)
  3. `RemoveNativePdbsFromPublish` (ADR-052) — matches on `.pdb` extension with NuGet `AssetType == 'native'`. Satellite resource assemblies are `.dll`, not `.pdb`, and are not native assets. Not affected.
- **Conclusion: nothing in the build/publish pipeline filters, trims, or excludes satellite assemblies today.** Adding 22 more `Strings.<culture>.resx` files is purely additive — the SDK will produce 22 more `<culture>/Parlotype.resources.dll` folders automatically, with no MSBuild changes required in the `.csproj`, `Directory.Build.props`, or `Directory.Build.targets`.

**Measured satellite size (existing languages, Debug build):**

| Culture | Path | Size |
|---|---|---|
| `es` | `src/Parlotype.Desktop/bin/Debug/net10.0/es/Parlotype.resources.dll` | 54,272 bytes (~53.0 KB) |
| `ru` | `src/Parlotype.Desktop/bin/Debug/net10.0/ru/Parlotype.resources.dll` | 66,048 bytes (~64.5 KB) |

Average ≈ 58.75 KB per language satellite. **Caveat:** these are Debug configuration outputs — no `Release/win-x64` publish output in the repo currently has `es`/`ru` satellite folders (the existing `bin/Release/net10.0/win-x64` build predates or bypassed the localization satellites entirely — it still carries unfiltered `onnxruntime_providers_cuda.dll`/`onnxruntime_providers_tensorrt.dll`, meaning it's a plain `dotnet build -r win-x64`, not a `dotnet publish`, and likely predates ADR-050/051's publish-time filters). Satellite resource assemblies contain only serialized resources (no IL to optimize), so Debug vs. Release size should not differ meaningfully, but this could not be directly verified against a current Release/publish output.

**Extrapolation for 22 more languages:** 22 × ~58.75 KB ≈ 1,292.5 KB ≈ **1.26 MB**. Against a published app size of ~278 MB, that is **≈0.45%** — a negligible delta. Even at a generous 2× margin (some languages carry more/longer strings than `ru`/`es`), the total stays under 0.1% × 278 MB × ~2 ≈ under 1%.

## 4. Guardrails that are culture-aware today

**`scripts/check-localization.ps1`** — the culture-extraction regex is:
```powershell
[regex]::Matches($registry, 'new\("(?<culture>[a-z]{2}(?:-[A-Za-z]+)?)"\s*,')
```
This matches any lowercase two-letter code, optionally followed by a `-Variant` suffix (e.g. `pt-PT` would also match, though §2 recommends against shipping variants). All 22 new codes (`bg`, `hr`, `cs`, `da`, `nl`, `et`, `fi`, `fr`, `de`, `el`, `hu`, `it`, `lv`, `lt`, `mt`, `pl`, `pt`, `ro`, `sk`, `sl`, `sv`, `uk`) are exactly two lowercase letters and match cleanly, including `mt` and `el`, which do not collide with anything else the regex excludes (only `en` is explicitly filtered out via `Where-Object { $_ -ne 'en' }`). **Passes unchanged** — no script edit needed. The rest of the script (key parity, placeholder parity, hardcoded-literal baseline scan) is language-count-agnostic; it just loops over however many cultures the regex finds, so 3 → 25 is a pure scale-up, not a code change.

**`scripts/gen-strings.ps1`** — reads only the neutral `Strings.resx` to generate `Strings.cs`; it has no awareness of `SupportedUiLanguages` or of how many satellite locales exist. **Passes unchanged**, completely unaffected by adding languages.

**`src/Parlotype.Desktop.Tests/LocalizationParityTests.cs`**:
- `EveryRegisteredLanguage_HasAResourceFile`, `EveryLanguage_TranslatesEveryKey`, `EveryLanguage_HasNoOrphanKeys`, `EveryLanguage_KeepsThePlaceholdersItWasGiven` all iterate `SupportedUiLanguages.Translated` via the `TranslatedCultures()` `TheoryData` source — these **pass unchanged mechanically** and will automatically enforce the same rules against all 22 new `Strings.<culture>.resx` files the moment `SupportedUiLanguages.All` lists them and the resx files exist. No test code changes needed for these four.
- **`NoKeyIsAccidentallyUntranslated`** — this one needs attention. Its `deliberatelyIdentical` array is a single flat `string[]` shared across **every** language in the same loop (`foreach (var language in SupportedUiLanguages.Translated) { ... .Where(pair => !deliberatelyIdentical.Contains(pair.Key)) ... }`). Adding one key to this array to legitimately exempt (say) a Dutch string that happens to match English verbatim would silently also exempt that same key for all 24 other languages, including languages with no linguistic reason to share that string. At 2 translated languages this design was harmless (the array is empty today — "nothing is currently identical"); at 24 languages the blast radius of any single exemption is 12× larger, and a real missed translation in, say, Ukrainian could hide behind an exemption added for a legitimately-identical Danish string. **This is a design gap that should be fixed as part of the expansion**: change `deliberatelyIdentical` from `string[]` to a `Dictionary<string, string[]>` keyed by culture (or a `(string culture, string key)` set), so an exemption only ever applies to the one language it was written for. This is not a hard blocker — the test still compiles and runs correctly for 25 languages — but it weakens as languages scale and should be called out explicitly rather than deferred silently.
- `GeneratedAccessor_CoversEveryKey_InBothDirections` and `EveryCompositeKey_HasATypedFormatHelper` operate only on the neutral resx and the generated `Strings.cs`; **unaffected** by language count.
- `EveryTrKeyUsedInMarkup_ExistsInTheResx` and `NoNewHardcodedUiText_InAxaml` scan `.axaml` files against the neutral resx/baseline; **unaffected** by language count.

**`src/Parlotype.Desktop.Tests/LocalizationTests.cs`** — the Russian-only assertions:
- `AssertReadsAsRussian` (private helper), `EveryReservedShortcut_IsTranslated`, `EveryHotkeyConflictReason_IsTranslated`, and `EveryCloudFailure_IsTranslated` all hard-code `Localizer.Instance.SetCulture(Russian)` and assert the resulting text contains Cyrillic (`c is >= '\u0400' and <= '\u04FF'`). These **pass unchanged** when 22 languages are added, and — importantly — **do not need to be duplicated per language** to stay effective: their job is to be a canary that a given enum member (`ReservedShortcut`, `HotkeyConflictReason`, `CloudSpeechErrorKind`, `CloudBaseUrlError`, `CloudConfigurationError`) actually has a corresponding key in the neutral `Strings.resx` at all — the comment in the file explains that a missing mapping falls through to Core's invariant English wording for *every* language simultaneously, which the resx-key parity tests cannot detect (all languages trivially "agree" by all being wrong the same way). Once the Russian canary forces the key to exist in the neutral resx, `LocalizationParityTests.EveryLanguage_TranslatesEveryKey`/`EveryLanguage_KeepsThePlaceholdersItWasGiven` already force all 24 translated languages (not just Russian) to carry that key with matching placeholders. The one caveat: parity only checks that a key exists and its placeholders match — it does not verify that a language's translation is actually *its own language's script* the way `AssertReadsAsRussian` does for Russian. There is no equivalent live check today that (e.g.) the Slovenian translation of `ReservedShortcut.LockWorkstation` is actually Slovenian rather than a stray copy-paste of English or another language — `NoKeyIsAccidentallyUntranslated`'s length threshold (`>25` chars) would catch a full-sentence copy but not a short one. This is an acceptable, pre-existing gap (it already exists for `es`) rather than something introduced by scaling to 25 — no code change is strictly required, but note it as a known coverage limit.

## 5. Text expansion risk

**Read:** `src/Parlotype.Desktop/Resources/Strings.resx` in full (1,335 lines).

- **Total key count:** 389 (`<data name="...">` entries). *(Corrected 2026-09-17 during Phase 0: the original 392 came from a `grep -c` that did not agree with a parse. Four independent counts — `grep -o | wc -l`, Python `ElementTree`, .NET `XmlDocument.SelectNodes`, and the importer's own run — all return 389.)*
- **Keys with `{0}`/`{1}`/`{2}`-style placeholders:** 47.
- **Keys with a `<comment>` element:** 140.

**15 longest English values** (character count, key, text):

| Chars | Key | Text (verbatim, truncated where noted) |
|---|---|---|
| 403 | `Onboarding_Tray_Body` | "Hiding or closing the widget does not quit Parlotype — it keeps listening for your hotkeys from the system tray. Click the tray icon to bring the widget back. To exit completely, right-click the tray icon and choose Exit. So your hotkey works from the moment you sign in, Parlotype starts with Windows and waits in the tray — no window opens. Turn that off any time in Settings → Application → Startup." |
| 339 | `Settings_Prompts_Help_BuiltInBody` | "The built-in default carries three bodies and the engine picks one per transcription: a transcription body, where the output language equals the spoken one, so {speech_lang} appears twice — "speech in X into X text"; a translation body that pairs {speech_lang} with {text_lang} for the output; and an auto-detect body with no placeholders." |
| 311 | `Onboarding_Widget_Body` | "This small always-on-top window is your dictation control. The big button starts and stops recording; hover the card to see the current status. Drag the grip strip at the top to move it anywhere, and the ✕ button hides it to the tray. Engines that offer a language choice show a language strip along the bottom." |
| 309 | `Settings_CloudProviders_Intro` | "Bring your own API key. When a cloud engine is active, your audio recordings are sent to the configured provider for transcription — nothing runs locally for that engine. Billing is directly between you and the provider; Parlotype is not a billing intermediary (ADR-032, "Local by default. Cloud by choice.")." |
| 286 | `Settings_ParakeetModel_Description` | "NVIDIA Parakeet runs fully on the CPU — no GPU needed. It transcribes 25 European languages (detected automatically) with punctuation and capitalization. INT8 is the recommended default: small and fast. Full precision (fp32) is slightly more accurate but 4× larger and slower to decode." |
| 275 | `Onboarding_Welcome_Body` | "Parlotype turns speech into text anywhere on your computer: dictate into any app, and the recognized text is typed right where your cursor is. By default, recognition runs entirely on this machine — your audio never leaves it unless you deliberately switch to a cloud engine." |
| 264 | `Settings_Updates_WhatSendsBody` | "An anonymous request to api.github.com for the public list of Parlotype releases, and a download of the release file itself. No account, no machine identifier, no usage data. This is the only network request Parlotype makes while all your speech engines are local." |
| 249 | `Settings_Prompts_Help_TranslationBody` | "Only when all three hold: the Translate toggle is on, an output language is selected, and it differs from the spoken language (Auto-detect always counts as different). Otherwise the engine just transcribes, and the output language is the spoken one." |
| 246 | `Onboarding_Engine_Body` | "Parlotype ships several recognition engines — switch between them in the highlighted list. Parakeet v3 is the default: fast, fully on-device, and it auto-detects 25 European languages. Each engine adds its own pages to the navigation on the left." |
| 240 | `Onboarding_Cloud_Body` | "The two cloud engines are off by default and require your own API key. While one is active, your audio is sent to that provider for transcription and the widget shows a persistent Cloud badge. Everything else in Parlotype stays fully local." |
| 239 | `Onboarding_Model_Body` | "Each local engine offers models of different sizes — larger models are more accurate but slower and take more disk space. Model files are downloaded automatically the first time they are used; models already on disk are marked in the list." |
| 222 | `Settings_Startup_WhatChangesBody` | "A single entry under your own user account (HKCU Run), pointing at Parlotype. No service, no scheduled task, no administrator rights, nothing machine-wide. You can see and remove it yourself in Task Manager → Startup apps." |
| 218 | `Settings_Engine_OpenAiCompatible_Description` | "Cloud transcription via your own OpenAI, Groq, or compatible API key. Audio is sent to the configured provider — nothing runs locally. Fast even on weak hardware. Opt-in; configure your key under Cloud providers below." |
| 208 | `Settings_Engine_Gemma4SectionNote` | "Gemma 4 E4B runs via a local llama-server process (llama.cpp, Vulkan). English transcription only. Works best with clean speech and a good microphone. The model files (~10 GB) will be downloaded on first use." |
| 207 | `Settings_LlamaCpp_PackFolderWarningBody` | "Uninstalling Parlotype — or re-running its installer — deletes its install folder and everything under it, including this llama-server build. Move the build somewhere else and point this at the new location." |

These longest strings are all multi-sentence body/help text, not the short button/label copy where expansion is proportionally worst — the standard localization guidance (commonly cited Microsoft/IBM UI-localization tables) is that **percentage expansion is inversely related to source length**: very short strings (1–10 English characters) can expand 100–300%, medium strings (11–20 chars) 60–100%, and long paragraphs (70+ chars, like the ones above) only ~30%. The risk in this app is concentrated in short labels, buttons, and nav headers (e.g. "Microphone", "Theme", "Startup") rather than in these long bodies.

**Languages expected to expand most for short UI strings, among the 22 being added** (approximate, based on standard localization-industry guidance for Latin/Cyrillic/Greek European languages — not independently measured against this app's actual strings):
- **German (`de`)** and **Finnish (`fi`)** — classically the worst offenders for short UI copy due to compounding/agglutination; short English labels can expand 30–35%+ (e.g., a 2-word button label routinely becomes one long compound word).
- **Dutch (`nl`)** — similar compounding behavior to German, typically 20–25% expansion.
- Slavic/Baltic languages with heavy inflection (**Polish `pl`, Czech `cs`, Slovak `sk`, Slovenian `sl`, Croatian `hr`, Lithuanian `lt`, Latvian `lv`, Bulgarian `bg`, Ukrainian `uk`**) — typically 15–25% expansion for short strings, plus diacritics/wider Cyrillic glyphs that can affect line-wrapping even when character count is similar.
- **Greek (`el`)** — commonly cited at ~10–20% expansion, plus its glyphs render wider than Latin at the same font size in most UI fonts, which can matter more than the raw character-count delta.
- Romance languages already in the mix for reference (**French, Italian, Romanian, Portuguese**) — typically 15–30% expansion, similar to the already-shipped `es`.

**Recommendation for screenshot-testing:** prioritize **German (`de`)** as the primary worst-case expansion target (compounding + already a major European market), and **Finnish (`fi`)** as a secondary worst-case; add **Greek (`el`)** as a non-Latin-glyph-width check distinct from the Cyrillic check the app already exercises via `ru`. This is guidance from general localization practice, not a measurement against Parlotype's actual translated strings (which do not exist yet for these languages) — treat it as where to look first, not a guarantee.

## Artifacts

The verification console app used for §1/§2 was written to the scratchpad at
`C:\Users\Maksim\AppData\Local\Temp\claude\C--projects-mdemin729-parlotype\259c7f4c-ede0-4dd7-9a57-583c0049407d\scratchpad\cultureprobe\` (`Program.cs` + `cultureprobe.csproj`) and is not part of the repository.
