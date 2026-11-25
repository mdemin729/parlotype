# RU glossary

No per-language glossary file was created for this culture during the original 25-language wave (ru/es were translated before that convention started). This file starts as a stub recording only later additions; the existing `Strings.ru.resx` remains the source of truth for established terminology.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"По умолчанию (дословное распознавание)"** — verbatim/word-for-word rendered as *дословное (word-for-word)*, paired with the already-established *распознавание* for transcription; no new terms introduced.
- `Settings_SpeechCleanup_Title` ("Speech cleanup") → **"Редактура текста"** — *редактура* (editing) rather than *очистка*, which reads as deleting data; used in every speech-cleanup string, including `Settings_LlamaCpp_Description`. "Writing style" → **"Стиль текста"**, styles as masculine adjectives agreeing with *стиль*: Разговорный / Полуразговорный / Полуофициальный / Официальный. English "contractions" → *сокращения вроде «I'm»* (example added, since *сокращения* alone is ambiguous).

## macOS terms

The `_Mac` keys and the two permission dialogs use the names macOS itself shows in Russian (checked against Apple's ru-ru Mac User Guide), never literal translations. Each `_Mac` string is its Windows counterpart with only these terms swapped.

| English | Russian | Notes |
|---|---|---|
| menu bar | **строка меню** | Replaces *трей* in every `_Mac` key: «в строке меню», «в строку меню». |
| System Settings | **Системные настройки** | Button: «Открыть Системные настройки» (the wording of macOS's own permission prompt). |
| Privacy & Security | **Конфиденциальность и безопасность** | |
| Accessibility | **Универсальный доступ** | Title is «Parlotype нужен „Универсальный доступ“» in guillemets, not *доступ к универсальному доступу*. |
| Microphone | **Микрофон** | |
| General | **Основные** | |
| Login Items | **Объекты входа** | macOS 15 and later shows «Объекты входа и расширения»; the short name is kept, as in the English. |
| Applications folder | **папка «Программы»** | The Finder name; app in prose stays *приложение*. |
| log in | **вход** / **вход в систему** | «при входе в macOS», same as «при входе в Windows». |
| launch agent | **агент запуска** | |
| this copy | **эта копия** | Replaces *эта сборка* in the two not-installed states. |

Settings paths are quoted whole, as the Windows strings do: «Системные настройки → Основные → Объекты входа».
