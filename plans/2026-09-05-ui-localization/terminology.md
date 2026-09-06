# Terminology sheet

One fixed term per concept, held across all 331 keys. Extend this rather than improvising
a synonym — consistency across a settings window matters more than the best word for any
one string. This is also the reference for German, French and anything after them.

## Core concepts

| English | Russian | Spanish | Note |
|---|---|---|---|
| dictation / to dictate | диктовка / диктовать | dictado / dictar | |
| recording | запись | grabación | |
| speech engine | движок распознавания | motor de reconocimiento | "движок", not "механизм" |
| to transcribe / recognition | распознавать / распознавание | transcribir / reconocimiento | |
| model | модель | modelo | |
| hotkey | горячая клавиша | atajo de teclado | |
| widget | виджет | widget | |
| system tray | трей / системный трей | bandeja del sistema | |
| Settings (the window) | Настройки | Configuración | not "Ajustes" — neutral Spanish |
| source language / spoken | язык речи | idioma hablado | |
| target language / output | язык вывода | idioma de salida | |
| translation | перевод | traducción | |
| runtime (Whisper backend) | среда | runtime | ES keeps the English word |
| build (of llama-server) | сборка | compilación | |
| install (noun) | установка | instalación | |
| prompt | промпт | prompt | loanword in both |
| placeholder (in a prompt) | подстановка | marcador | |
| cloud provider | облачный провайдер | proveedor en la nube | |
| API key | API-ключ | clave de API | |
| to download | скачать / загрузить | descargar | |

## Register

- Terse, sentence case, no exclamation marks — same as the English.
- Russian: address the user as **вы** (lower case), imperative for actions
  («Выберите…», «Нажмите…»).
- Spanish: address the user as **tú** («Elige…», «Pulsa…»), consistent with the informal
  English voice.
- Both: guillemets «…» for quoting UI labels, not straight quotes.
- Spanish: opening `¿` and `¡` where the sentence needs them.
- Non-breaking-ish spacing before units in Russian (`{1} с`), none in English (`{1}s`).

## Never translated

Product and file identifiers, verbatim in every language:

`Parlotype` · `Whisper` · `Whisper.net` · `Parakeet` / `NVIDIA Parakeet` · `Gemma 4` ·
`llama.cpp` / `llama-server` / `llama-server.exe` · `Vulkan` · `CUDA` · `ONNX` · `CPU` ·
`GPU` · `OpenAI` · `Groq` · `xAI Grok` · `GitHub` · `Setup.exe` · `settings.json` ·
`HKCU Run` · `api.github.com` · `vulkan-1.dll` · `Esc` · model ids and sizes
(`Gemma 4 E2B (Q8_0)`, `~5.5 GiB`) · prompt tokens `{speech_lang}` / `{text_lang}`.

Windows UI names (`Task Manager` → «диспетчер задач» / "Administrador de tareas",
`Startup apps` → «Автозагрузка приложений» / "Aplicaciones de inicio") **are** translated —
but to the wording Windows itself uses in that language, not a fresh translation.

## Grammatical cases — the open dependency

Russian inflects nouns; English does not mark it. Every key that substitutes a **language
name** was therefore written so the slot sits where no case is needed — leading the
sentence, or after a colon or dash.

Today this is latent: `LanguageCatalog.GetEnglishName` returns English names ("Russian"),
which are indeclinable in a Russian sentence. **Phase 6 changes that** by localizing
language names through ICU, and at that moment these keys need re-reading:

| Key | Slot | Risk |
|---|---|---|
| `Language_ToggleSwitch_TranslateToFormat` | «Перевод на {0}» | **needs accusative** — the one genuinely unsafe slot |
| `Language_Summary_Format` | «Вы говорите: {0} → …: {1}» | safe (colons) |
| `Language_Toast_SourceUnsupportedFormat` | «{0} не поддерживается…» | safe (leads) |
| `Language_Toast_TargetUnsupportedFormat` | «Язык перевода — {2}.» | safe (dash) |
| `Language_Summary_*Format` | «{0} (без перевода)» | safe (leads) |
| `Language_Source_DetectedFormat` | «Определено: {0}» | safe (colon) |

Spanish needs no case but does need article/gender agreement in the same places; the same
colon-and-dash shapes avoid it.
