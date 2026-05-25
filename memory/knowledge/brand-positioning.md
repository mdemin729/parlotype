---
title: Brand Positioning — Local by Default, Cloud by Choice
type: knowledge
tags: [brand, positioning, privacy, product]
created: 2026-05-25
summary: Parlotype is positioned as "Local by default. Cloud by choice." — single app, local-default, opt-in cloud providers (BYOK), tagline preserved
---

# Brand Positioning

> **Local by default. Cloud by choice.**

Parlotype is a **single application** that runs speech recognition on-device by default. Cloud / online speech providers are an **opt-in** option for users whose hardware cannot deliver acceptable latency with local models. Source: [[../../docs/decisions/032-online-speech-providers-positioning|ADR-032]].

## Why this matters for AI agents

When writing or editing documentation, copy, ADRs, or vault notes:

- **Do not** use the phrases "privacy-first", "privacy-focused", or "voice data never leaves your machine" as unqualified product-wide claims. These were accurate before cloud providers were planned, but they overcommit now.
- **Do** use "**local by default**" / "**local-by-default**" / "**Local by default. Cloud by choice.**" as the headline framing.
- **Preserve** the tagline "**Speak freely. Type privately.**" — "privately" now means *the posture the user chose* (default = on-device), not an architectural lock-in.

## The 5 brand commitments

Every change touching cloud-provider features must keep all five:

1. **Local stays the default.** First-run users get the on-device experience without cloud configuration; cloud is never auto-selected or recommended at install time.
2. **Opt-in, not opt-out.** Cloud providers are off until the user explicitly enables one in Settings.
3. **Transparency.** Active cloud provider → visible in-app indicator + docs explaining what is sent, where it goes, retention, and revocation.
4. **Bring your own key (BYOK).** Data path is user ↔ provider; Parlotype is not a billing intermediary.
5. **Tagline preserved.** "Speak freely. Type privately." still holds — `privately` = the chosen posture.

## Why not rename / why not two SKUs

The name "Parlotype" was chosen as **privacy-neutral** (meaning "speak-type") in `docs/research/name_deliberation.md` — adding cloud providers does not invalidate the name. A single app keeps the brand cohesive and surfaces the local-vs-cloud trade-off in Settings, where the user has context to make it.

## Language replacements (canonical)

| Old | New |
|---|---|
| "privacy-first" | "local by default" |
| "voice data never leaves your machine" (unqualified) | "your voice never leaves your machine in local mode (the default)" |
| "All speech recognition runs on-device" | "On-device speech recognition is the default; cloud providers are opt-in" |
