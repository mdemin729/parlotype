---
status: accepted
date: 2026-11-11
supersedes: [013]
---

# 071. Project knowledge lives in docs/, the memory vault and session journal are retired

## Context

[ADR-013](013-obsidian-memory-vault.md) created an Obsidian vault under `memory/` as the
agents' persistent memory: a router, per-directory `_index.md` files, per-project service
profiles, conventions, an ADR index, knowledge notes and a session journal with a
start/end protocol. A full audit of the vault on 2026-11-11 found that it cost more than it
returned:

- **The session journal held no live state.** All 68 session notes (45 current, 23
  archived, ~330 KB) concerned merged work. Their "Next Action" items were done, and
  nearly every "Facts Learned" entry was already in a knowledge note. Eight notes held a
  fact worth keeping, and those facts fit in a handful of lines.
- **The service profiles restated the code and drifted.** `memory/services/*.md` (about
  80 KB) were symbol inventories that agents dutifully updated, by appending a paragraph
  per change and never revising old statements. They still named types that no longer
  exist (`IVoiceActivityDetector`), a removed project (`Parlotype.Gemma4`), the removed
  CUDA runtime, and a "zero dependencies" Core. One Glob over `src/` answers the same
  question correctly.
- **The ADR index was a second, unreviewed copy of the ADRs.** `memory/decisions/_index.md`
  was 80 KB, hand-written, with paragraph-length "Key Impact" cells (the longest near
  6 000 characters) and five broken links.
- **The instruction file every session loads was wrong in four places**: the project count,
  the hotkey hook class (`TaskPoolGlobalHook`, actually `SimpleGlobalHook`), the pipeline
  hand-off (`ConcurrentQueue<float[]>`, actually `System.Threading.Channels` since ADR-045),
  and settings views that do not exist. Facts were stated in up to eight places, so
  correcting one left the others wrong.
- **Two architecture homes** (`memory/architecture/` and `docs/architecture/`) duplicated
  each other, and the Obsidian-specific format (wikilinks, callouts, `last_updated`
  frontmatter, index and staleness scripts) served a tool that agents do not use.

The rule behind the drift was the Definition of Done item "update the vault when public
symbols change". It asked for new statements and never for corrections, so it produced
growth without accuracy.

## Decision

Retire the vault and the session journal. Everything worth keeping moves into plain
Markdown under `docs/`, each kind of knowledge in one place:

| Kind | Home | Rule |
|---|---|---|
| Decisions | `docs/decisions/` | One ADR per decision. The body is immutable once accepted; the frontmatter (`status`, `supersedes`, `amends` and their reverses) is kept current. |
| How the system is put together, and why | `docs/architecture/` | Living documents, no per-class inventories. |
| Gotchas not derivable from code | `docs/knowledge/` | One fact per note, frontmatter `title`, `area`, `summary`, `verified`. |
| Research that fed a decision | `docs/research/YYYY-MM-DD-topic/` | Dated snapshots, not updated afterwards. |
| Translator material | `docs/localization/` | Glossaries, brief, terminology, outstanding items. |
| Work in flight | `plans/` | One folder per live plan, deleted on completion (see `plans/WORKFLOW.md`). |

Supporting rules:

- **Correct, do not append.** When a change makes a statement in `docs/architecture/`,
  `AGENTS.md`, a skill or a knowledge note false, that statement is corrected or deleted in
  the same change. Adding a new paragraph next to a false one does not count.
- **Indexes are generated.** `docs/decisions/README.md` and `docs/knowledge/README.md` are
  produced from frontmatter by `scripts/gen-docs-index.ps1`; `-Check` fails when either is
  stale or an ADR relation is one-sided. Nobody edits them by hand.
- **No session journal.** Git history and the pull request description are the record of
  what was done. A fact learned along the way goes into `docs/knowledge/`; an unfinished
  piece of work goes into a plan.
- **Plans are deleted on completion.** The ADR records the decision, research worth keeping
  is promoted to `docs/research/` or `docs/localization/`, and the plan folder is removed
  in the same change.
- **`AGENTS.md` stays short and tool-neutral.** It holds the rules an agent would otherwise
  get wrong and points at `docs/` for the rest. Claude Code reads it through `CLAUDE.md`
  (`@AGENTS.md`); other agents read it directly.

The `session-management` and `obsidian-markdown` skills, the vault scripts and the
Obsidian configuration are removed with the vault. The remaining skills and the
`.claude/agents/` definitions were rewritten to point at the new locations.

## Consequences

**Easier:**

- Each fact has one home, so correcting it once corrects it everywhere.
- The always-loaded `AGENTS.md` dropped from 23 KB to under 10 KB.
- Indexes cannot drift from the files they list, and CI can check that.
- GitHub renders everything; no editor-specific syntax.

**More difficult:**

- There is no handoff note to resume from. Work that spans sessions must be written down as
  a plan, and a session that ends mid-change must leave the state in the plan or the PR.
- Nothing prompts an agent to record a fact; the Definition of Done does, and only when
  the agent judges the fact non-derivable.
- The vault's history is reachable only through git (`git log -- memory/`).
