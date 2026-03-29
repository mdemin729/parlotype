---
title: Obsidian Markdown Skill
type: skill
status: active
tags: [skill, obsidian, markdown]
last_updated: 2026-03-28
summary: Teaches agents to use Obsidian-flavored markdown correctly in the memory vault
---

# Obsidian Markdown Skill

## Wikilinks
Use `[[note-name]]` to link between vault notes. Use `[[note-name|Display Text]]` for custom display text.

```markdown
See [[audio-pipeline]] for details.
Related to [[decisions/_index|ADR-003]].
```

## Frontmatter
Every note must have YAML frontmatter with at minimum:
```yaml
---
title: "Note Title"
type: service-profile | architecture | convention | index | session | knowledge | skill
status: active | deprecated | draft
last_updated: YYYY-MM-DD
summary: "One-line description"
---
```

## Headings
- Use H1 (`#`) for the note title (once per note)
- Use H2 (`##`) for major sections — each section should be independently searchable
- Use H3+ for subsections within a major section

## Tables
Prefer markdown tables for structured data. Keep columns aligned.

## Callouts
Use Obsidian callouts for important information:
```markdown
> [!warning] Breaking Change
> This API changed in v2.0.

> [!tip] Performance
> Use beam size 1 for fastest transcription.

> [!note]
> This convention applies to Platform only.
```

## Tags
Use frontmatter `tags` array, not inline `#tags`:
```yaml
tags: [audio, vad, whisper]
```

## Embeds
Embed another note's content with `![[note-name]]`. Use sparingly — prefer links.
