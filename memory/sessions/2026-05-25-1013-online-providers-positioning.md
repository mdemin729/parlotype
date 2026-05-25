---
title: "Session: 2026-05-25 — Online Providers Positioning"
type: session
status: active
tags: [brand, positioning, adr, docs, online-providers]
created: 2026-05-25
summary: Reframed Parlotype from "privacy-first / local-only" to "Local by default. Cloud by choice." in preparation for opt-in cloud speech providers. Docs/vault only, no code.
---

# Session: 2026-05-25 — Online Providers Positioning

## Active Focus
- `docs/decisions/032-online-speech-providers-positioning.md` (new ADR)
- `memory/knowledge/brand-positioning.md` (new knowledge note)
- `README.md` — intro tagline area + new "Provider Modes" sub-section under "Speech Engines"
- `CLAUDE.md` (project root, line 7) — overview sentence reframed
- `memory/CLAUDE.md` — "Privacy-first" constraint replaced with "Local-by-default"
- `memory/architecture/audio-pipeline.md` — added cloud-provider extension-point callout
- `memory/services/core.md` — flagged `SpeechEngine` for future cloud values
- `memory/decisions/_index.md` + `memory/knowledge/_index.md` — new rows + `last_updated` bumped

## Decisions Made
- **Single application**, not two SKUs. No rename. The product remains **Parlotype**; the name was deliberately chosen as privacy-neutral ("speak-type") and stays valid.
- **Positioning principle:** "**Local by default. Cloud by choice.**"
- **5 brand commitments** must hold for every future cloud-provider change: (1) local stays the default, (2) opt-in not opt-out, (3) transparency (visible indicator + docs), (4) BYOK (no Parlotype-hosted billing), (5) tagline preserved.
- **Tagline kept:** "Speak freely. Type privately." — "privately" now describes the user's chosen posture (default = on-device), not an architectural lock-in.
- **Language convention:** drop "privacy-first" / unqualified "voice data never leaves your machine"; replace with "local by default" / scope-qualified phrasings. Canonical table lives in `memory/knowledge/brand-positioning.md`.
- **Explicit deferrals** (documented in ADR-032 Out-of-Scope section): which providers ship first, secure key storage, settings UI, `ISpeechRecognizer` shape for streaming cloud APIs, visible-indicator UX, telemetry, fallback behaviour when cloud unreachable, hosted/billed mode, separate distribution channel.

## Facts Learned
- `memory/decisions/_index.md` has a **pre-existing duplicate** of number 031 — both ADR-031 entries (`031-github-release-strategy` and `031-benchmark-warmup-pass`) are listed. Not touched in this session (out of scope), but the next ADR-numbering author should be aware: I used `032` here, so the next free number is **033** regardless of which `031` keeps its slot.
- `ISpeechRecognizer` (`src/Parlotype.Core/Speech/ISpeechRecognizer.cs`) is a clean integration seam: adding cloud providers requires only a new `SpeechEngine` enum value + a new Platform recognizer + a new branch in `SpeechRecognizerFactory.GetRecognizerAsync`. No Core interface change anticipated for the basic batch case (`ReadOnlyMemory<float>` → `TranscriptionResult`), though streaming cloud APIs may motivate a `IStreamingSpeechRecognizer` extension later.
- The product name "Parlotype" is **privacy-neutral by design** — `docs/research/name_deliberation.md` documents a 5-person multilingual panel that picked it because it means "speak-type" in Romance languages, not because of any privacy connotation. This is why the cloud-providers shift does not require a rename.

## Open Blockers
- None. This session is docs-only and self-contained.

## Documentation Status
- ADR: done — `docs/decisions/032-online-speech-providers-positioning.md`
- Vault (services/architecture): done — `memory/CLAUDE.md`, `memory/architecture/audio-pipeline.md`, `memory/services/core.md`, `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/brand-positioning.md` + index row

## Next Action

Start the **first technical plan** for cloud / online speech providers. Suggested scope for that plan (in priority order):

1. **Provider selection & pricing/latency comparison** — research 2–3 candidate providers (OpenAI Whisper API, Groq, Deepgram are the obvious starting set). Decide which 1 lands first as a vertical slice. Capture in a new ADR.
2. **Secure key storage on Windows** — DPAPI vs Windows Credential Vault vs encrypted JSON. ADR + small Core/Platform spike.
3. **`ISpeechRecognizer` extension shape** — confirm the existing batch signature is enough for the first provider, or design `IStreamingSpeechRecognizer` if the chosen provider is streaming-first.
4. **Settings UI** — provider picker + key entry + visible-indicator pattern (the transparency commitment).
5. **Fallback behaviour** — what happens when the cloud is unreachable: silent local fallback, hard fail, or user prompt? This is itself an ADR-worthy decision because it affects the "opt-in" commitment.

All five must honour the 5 brand commitments in [[../../docs/decisions/032-online-speech-providers-positioning|ADR-032]].
