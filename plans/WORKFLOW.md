# Plans & Decisions Workflow

Instructions for the AI agent on managing plans and architecture decision records.

## Folder Structure

```
plans/
├── INDEX.md              # Active plans only (in_progress + planned)
├── WORKFLOW.md           # This file
├── _template.md          # Plan template
└── YYYY-MM-DD-feature/   # One folder per plan (flat, no subfolders)
    ├── task.md            # Primary file — has YAML frontmatter
    ├── implementation-plan.md
    └── research.md        # Optional

docs/decisions/
├── _template.md          # ADR template
└── NNN-title.md          # Numbered decisions
```

## Plan Frontmatter

Every plan's primary file (usually `task.md`, or `implementation-plan.md` if no task.md exists) must have YAML frontmatter:

```yaml
---
title: Short descriptive title
status: planned          # planned | in_progress | completed | abandoned
created: YYYY-MM-DD
started:                 # date when work began
completed:               # date when finished
---
```

## Workflows

### Creating a New Plan

1. Create folder: `plans/YYYY-MM-DD-kebab-case-name/`

   > **Do NOT** create a bare `plans/YYYY-MM-DD-name.md` file. The dated **folder** is required, with `task.md` inside it.

2. Create primary file (e.g., `task.md`) with frontmatter (`status: planned`)
3. Add row to `plans/INDEX.md` under **Planned** section
4. Add supporting files as needed (`research.md`, `implementation-plan.md`)

### Starting Work on a Plan

1. Update frontmatter: `status: in_progress`, set `started: YYYY-MM-DD`
2. Move row in `plans/INDEX.md` from **Planned** to **In Progress**

### Completing a Plan

1. Update frontmatter: `status: completed`, set `completed: YYYY-MM-DD`
2. Remove row from `plans/INDEX.md`
3. Create an ADR (`docs/decisions/NNN-title.md`) capturing significant architectural decisions made during implementation — see [Creating an ADR](#creating-an-adr)
4. Do NOT move the folder — it stays in `plans/`
5. Review any `store_memory` calls made during the session. If a fact is durable and broadly useful (architecture, conventions, subsystem descriptions), add it to the appropriate section of `CLAUDE.md` rather than relying solely on `store_memory` (which is server-side, not version-controlled, and subject to a retention window)

### Abandoning a Plan

1. Update frontmatter: `status: abandoned`, set `completed: YYYY-MM-DD`
2. Remove row from `plans/INDEX.md`
3. Add a brief reason in the file body under an `## Abandoned` heading

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
| [YYYY-MM-DD-name](YYYY-MM-DD-name/) | YYYY-MM-DD | Brief description |

## Planned

| Plan | Created | Description |
|------|---------|-------------|
| [YYYY-MM-DD-name](YYYY-MM-DD-name/) | YYYY-MM-DD | Brief description |
```

Only non-completed plans appear here. Git history preserves removed rows.

## ADR Statuses

- `proposed` — under discussion
- `accepted` — approved and in effect
- `deprecated` — no longer relevant
- `superseded` — replaced by a newer ADR (link to successor)
