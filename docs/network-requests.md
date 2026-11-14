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
| llama.cpp release list (Gemma 4 engine and speech cleanup) | `api.github.com`: `/repos/ggml-org/llama.cpp/releases` for the newest builds, then `/releases/latest` for the build llama.cpp currently calls its release, and `/releases/tags/b{N}` for that build's files when the first answer no longer lists it, about once per llama.cpp release ([ADR-074](decisions/074-prefer-llama-cpp-release-build.md)) | The first time the Settings window opens in a session, at most once an hour (the answers are cached on disk), and when you press **Check for updates** on the llama.cpp server page. The same cached list is read when you turn speech cleanup on and no llama.cpp server is installed; it is fetched then only if the cache is more than an hour old. | Two or three anonymous GETs with `User-Agent: parlotype/<assembly version>`. The build number in the third path comes from GitHub's own answer. |

## Only when you ask for it

| What | Host | When | What is sent |
|---|---|---|---|
| Speech model download | `huggingface.co` | When a model you chose, or the default Parakeet model on first dictation, is not on disk yet. A dialog shows the download and can cancel it. | Anonymous GETs for the model files. |
| Speech cleanup model download | `huggingface.co` (`superwhisper/s1-mini-GGUF`) | When you turn on **Settings → Speech cleanup**, or press Download there, and the model is not on disk yet. A dialog asks first, shows the download and can cancel it ([ADR-073](decisions/073-speech-cleanup-s1-mini.md)). | An anonymous GET for the model file. |
| llama.cpp server download | `github.com` | When you install a llama-server build in Settings → llama.cpp, or turn speech cleanup on with no server installed. A dialog asks first. | An anonymous GET for the release asset. |
| Cloud transcription | The provider you configured (OpenAI, Groq, xAI or any compatible host) | Each dictation, only while a cloud engine is selected ([ADR-043](decisions/043-cloud-speech-providers-v1.md)). | The recorded audio and your API key, to that provider. |
| Feedback | `parlotype.dev` (`POST /api/feedback`) | Once per press of **Send** in the feedback window. Never automatically, in the background, on a crash or as a retry ([ADR-070](decisions/070-send-feedback-from-the-app.md)). | Your message, the kind (feedback or bug), your email if you typed one, and only the details you left ticked: app version, OS description, interface language. No logs, audio, transcripts, settings or keys; no `User-Agent`. The server does not store the IP address. |

Speech cleanup itself makes no network request: the text goes to a `llama-server` process on
`127.0.0.1` and nowhere else.

The website fallback in the feedback window opens `parlotype.dev/<lang>/feedback/` or
GitHub Issues in your browser; that is your browser's request, and the URL carries
nothing but the language.
