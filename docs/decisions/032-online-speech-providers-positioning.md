---
status: accepted
date: 2026-05-25
---

# 032. Online Speech Providers — Brand & Positioning

## Context

Parlotype has always been positioned as a **local-first, privacy-focused** voice-to-text app: all speech recognition runs on-device, and audio never leaves the user's machine. That positioning is repeated across `README.md`, the root `CLAUDE.md`, and `memory/CLAUDE.md` as an absolute architectural constraint.

In practice, local Whisper / Gemma 4 models do not deliver acceptable latency on every machine. Users with weaker CPUs, no GPU, or modest VRAM either pay a UX cost (multi-second transcription delays after they stop speaking) or downshift to smaller, less accurate models. Cloud speech providers (e.g. OpenAI Whisper API, Groq, Deepgram, Azure Speech) solve the latency/accuracy problem at the cost of sending audio off-device — a trade-off that some users will reasonably choose to make.

We therefore want to add **online / cloud speech providers** to Parlotype in a future code change. Before doing that, we need to decide *how the product is positioned* so the implementation lands in a brand frame that is honest, defensible, and does not contradict every doc the project has shipped to date. This ADR captures **positioning and brand commitments only**. Technical decisions (which providers, key storage, settings UI, etc.) are explicitly deferred.

## Decision

### Product shape

- **Single application**, not two SKUs. There will be no separate "Parlotype Local" and "Parlotype Online" builds, no separate websites, no rename. The product remains **Parlotype**.
- The existing `SpeechEngine` enum + `SpeechRecognizerFactory` are the natural extension point — cloud providers will appear as additional `ISpeechRecognizer` implementations alongside `WhisperSpeechRecognizer` and `LlamaCppSpeechRecognizer`.

### Positioning principle

> **Local by default. Cloud by choice.**
>
> Parlotype runs entirely on your device out of the box — your voice never leaves your machine. When you need more speed than your hardware can deliver, you can opt into a cloud speech provider with explicit consent and full transparency about what is sent and where.

### Brand commitments

These commitments must hold for every future change that touches cloud providers:

1. **Local stays the default.** A first-run user gets the on-device experience without any cloud configuration. Cloud is never auto-selected, never silently enabled, never the recommended default at install time.
2. **Opt-in, not opt-out.** Cloud providers are off until the user explicitly enables one in Settings.
3. **Transparency.** When a cloud provider is active, the UI surfaces a clear indicator (e.g. "Cloud: OpenAI") and the docs spell out what is transmitted, where it goes, the provider's retention policy, and how to revoke access.
4. **Bring your own key (BYOK).** Cloud providers connect directly using credentials the user supplies and controls. Parlotype is not a billing intermediary — the data path is user ↔ provider, not user ↔ Parlotype ↔ provider.
5. **Tagline preserved.** "**Speak freely. Type privately.**" still holds. "Privately" now describes *the privacy posture the user chose* (default = on-device); it no longer asserts an architectural lock-in.

### Language conventions

Replace absolute claims with the new framing across all docs and vault notes:

| Old wording | New wording |
|---|---|
| "privacy-first" / "Privacy-first" | "local-by-default" / "local by default" |
| "voice data never leaves your machine" | "your voice never leaves your machine in local mode (the default)" — or scope-qualify per context |
| "All speech recognition runs on-device" | "On-device speech recognition is the default; cloud providers are opt-in" |

### Why not two products / why not a rename

The name **Parlotype** was deliberately chosen as a **privacy-neutral** name meaning *"speak-type"* (see `docs/research/name_deliberation.md`). It does not encode "private" in its etymology, so adding online providers does not invalidate the name. Splitting into two SKUs would double maintenance, fragment the brand, and force users to pick at download time instead of at runtime where the trade-off is actually visible.

## Consequences

### Easier

- Future code work can introduce cloud `ISpeechRecognizer` implementations without contradicting any landed documentation.
- Users keep one product, one website, one releases page; the choice between local and cloud lives in Settings where the trade-off is in context.
- The "privacy-first" users we have today are not betrayed — local stays the default and behaves exactly as before. They never have to interact with cloud features.

### Harder

- We can no longer make the unqualified claim "voice data never leaves your machine" in marketing copy. The new framing requires one extra qualifier ("in local mode, the default") — slightly less punchy.
- Every future cloud-provider change must re-check the brand commitments above, because shipping a default-on, opt-out, or hosted-billing flow would silently violate this ADR.
- We commit to a transparency UX bar (visible indicator + clear docs) for every cloud provider we add — that work is not free.

### Out of scope (deferred to follow-up ADRs / plans)

This ADR explicitly does **not** decide:

- Which cloud providers ship first (OpenAI Whisper API, Groq, Deepgram, AssemblyAI, Azure, etc.).
- The `ISpeechRecognizer` extension shape for streaming / chunked cloud APIs.
- Secure storage for API keys (e.g. Windows DPAPI, credential vault).
- Settings UI for provider selection, model selection within a provider, and key entry / rotation.
- The visible-indicator UX (tray icon variant, transcribe-window badge, settings banner, etc.).
- Telemetry / cost / usage reporting in-app.
- Fallback behaviour when a cloud provider is unreachable (silent local fallback? hard fail? user prompt?).
- Whether to ever offer a Parlotype-hosted (non-BYOK) billing mode.
- Whether to publish a separate distribution channel (e.g. a hosted web app) under the same brand.

Each of these will need its own plan and, where it changes Core contracts / Platform registrations / external dependencies, its own ADR per the Definition of Done in `CLAUDE.md`.
