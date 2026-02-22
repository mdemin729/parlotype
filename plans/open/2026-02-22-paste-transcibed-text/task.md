## Context

1. Read @plans/open/2026-02-22-paste-transcibed-text/research.md
2. Read @docs/research/Local Voice-To-Text App Development.md

## Task

Implement the text injection feature for Parlotype on Windows using two different approaches:
1. using the `SharpHook` library - repository located at @D:\projects\TolikPylypchuk\SharpHook 
2. the clipboard-with-restore approach

They should use the same interface so we can easily switch between them.
The clipboard approach should be the default, with SharpHook as an optional alternative for users who prefer it.
User can choose it by specifying a command line argument (`--text-injection-mode=sharp-hook`).
DO NOT add UI selection for it.

## Implementation plan

1. Create the implementation plan in a new file `plans/open/2026-02-22-paste-transcibed-text/implementaion-plan.md`
2. Request my approval
3. Commit `research.md`, `task.md`, and `implementaion-plan.md`
4. Implement the first part of the task (using the `SharpHook` library)
5. Implement the second part of the task (the clipboard-with-restore approach)
