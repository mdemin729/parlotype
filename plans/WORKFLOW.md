# Plans & Decisions Workflow

Instructions for the AI agent on managing plans and architecture decision records.

**Policy:** `plans/` holds only live work. A plan exists in `plans/` while it is planned or
in progress. When it completes (or is abandoned) its folder is deleted in the same change.
The permanent record is the ADR (plus `CHANGELOG.md` for user-facing work); git history is
the archive. There is no `plans/archive/`.

## Folder Structure

```
plans/
├── INDEX.md              # Planned and in-progress plans only
├── WORKFLOW.md           # This file
├── _template.md          # Plan template
└── YYYY-MM-DD-feature/   # One folder per live plan (flat, no subfolders)
    ├── task.md            # Primary file — has YAML frontmatter
    ├── implementation-plan.md   # Optional
    └── research.md        # Optional

docs/decisions/
├── _template.md          # ADR template
└── NNN-title.md          # Numbered decisions

docs/research/
└── YYYY-MM-DD-topic/     # Research and measurements kept after a plan is deleted
```

## Plan Frontmatter

Every plan's primary file is `task.md` and must have YAML frontmatter:

```yaml
---
title: Short descriptive title
status: planned          # planned | in_progress
created: YYYY-MM-DD
started:                 # date when work began
---
```

Only `planned` and `in_progress` exist: a completed or abandoned plan is deleted, not
re-labelled.

## Workflows

### Creating a New Plan

1. Create folder: `plans/YYYY-MM-DD-kebab-case-name/`

   > **Do NOT** create a bare `plans/YYYY-MM-DD-name.md` file. The dated **folder** is required, with `task.md` inside it.

2. Create `task.md` with frontmatter (`status: planned`)
3. Add a one-line row to `plans/INDEX.md` under **Planned**
4. Add supporting files as needed (`research.md`, `implementation-plan.md`)

### Starting Work on a Plan

1. Update frontmatter: `status: in_progress`, set `started: YYYY-MM-DD`
2. Move the row in `plans/INDEX.md` from **Planned** to **In Progress**

### Completing a Plan

In one change:

1. Create an ADR (`docs/decisions/NNN-title.md`) capturing the significant decisions — see [Creating an ADR](#creating-an-adr). Add a `CHANGELOG.md` entry when the work is user-facing.
2. Move anything still worth reading to a stable home: measurements, benchmarks and
   research go to `docs/research/YYYY-MM-DD-topic/` (keep the plan's date prefix); translator
   material goes to `docs/localization/`; durable facts go to `docs/knowledge/`. Use
   `git mv` so history follows, and point ADR links at the new location.
3. Remove the row from `plans/INDEX.md`.
4. `git rm -r plans/YYYY-MM-DD-name/`.
5. Check that nothing still links to the deleted folder (`grep -rn "plans/YYYY-MM-DD-name" .`
   excluding `.claude/worktrees/`).

### Abandoning a Plan

1. Record the reason in the commit message (or in an ADR if the decision is worth keeping).
2. Remove the row from `plans/INDEX.md` and `git rm -r` the folder, as above.

### Creating an ADR

1. Find the next number: check `docs/decisions/` for the highest `NNN-` prefix
2. Create `docs/decisions/NNN-kebab-case-title.md` using the template
3. Set `status: accepted` (or `proposed` if pending discussion)

## INDEX.md Format

```markdown
# Plans

## In Progress

| Plan | Started | Description |
|------|---------|-------------|
| [YYYY-MM-DD-name](YYYY-MM-DD-name/) | YYYY-MM-DD | One sentence |

## Planned

| Plan | Created | Description |
|------|---------|-------------|
| [YYYY-MM-DD-name](YYYY-MM-DD-name/) | YYYY-MM-DD | One sentence |
```

Keep descriptions to one sentence; the plan's own `task.md` carries the detail. There is no
Completed section.

## ADR Statuses

- `proposed` — under discussion
- `accepted` — approved and in effect
- `deprecated` — no longer relevant
- `superseded` — replaced by a newer ADR (link to successor)
