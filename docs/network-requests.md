# Network requests

Every request Parlotype can make, what starts it, and what it carries. Speech
recognition runs on this machine by default; nothing below sends audio or text
unless you chose a cloud engine or pressed Send in the feedback window.

Keep this file, the README's [Updates](../README.md#updates) section and Settings →
Updates (`Settings_Updates_Network*` in `Strings.resx`) saying the same thing. A new
outbound request is a product decision and needs an ADR (see ADR-053 and ADR-070).

## On its own

| What | Host | When | What is sent |
|---|---|---|---|
| Update check | `api.github.com` (`/repos/mdemin729/parlotype/releases`), then `github.com` for the release file | Installed builds only: ~30 s after launch and every 6 hours, while **Settings → Updates → Check for updates automatically** is on (default). Off means no request. | An anonymous GET. No account, machine id, install id or usage data ([ADR-053](decisions/053-velopack-packaging-and-auto-update.md)). |

## When you open Settings

| What | Host | When | What is sent |
|---|---|---|---|
| llama.cpp release list (Gemma 4 engine) | `api.github.com` (`/repos/ggml-org/llama.cpp/releases`) | The first time the Settings window opens in a session, at most once an hour (the answer is cached on disk). | An anonymous GET with `User-Agent: parlotype/<assembly version>`. |

## Only when you ask for it

| What | Host | When | What is sent |
|---|---|---|---|
| Speech model download | `huggingface.co` | When a model you chose, or the default Parakeet model on first dictation, is not on disk yet. A dialog shows the download and can cancel it. | Anonymous GETs for the model files. |
| llama.cpp server download | `github.com` | When you install a llama-server build in Settings → llama.cpp. | An anonymous GET for the release asset. |
| Cloud transcription | The provider you configured (OpenAI, Groq, xAI or any compatible host) | Each dictation, only while a cloud engine is selected ([ADR-043](decisions/043-cloud-speech-providers-v1.md)). | The recorded audio and your API key, to that provider. |
| Feedback | `parlotype.dev` (`POST /api/feedback`) | Once per press of **Send** in the feedback window. Never automatically, in the background, on a crash or as a retry ([ADR-070](decisions/070-send-feedback-from-the-app.md)). | Your message, the kind (feedback or bug), your email if you typed one, and only the details you left ticked: app version, OS description, interface language. No logs, audio, transcripts, settings or keys; no `User-Agent`. The server does not store the IP address. |

The website fallback in the feedback window opens `parlotype.dev/<lang>/feedback/` or
GitHub Issues in your browser; that is your browser's request, and the URL carries
nothing but the language.
