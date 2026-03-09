---
status: accepted
date: 2026-03-04
---

# 009. Benchmark CLI Design

## Context

Evaluating Whisper transcription quality and performance requires a reproducible benchmark framework. Need to run the same audio samples through different configurations (models, parameters, VAD settings) and compare results quantitatively using Word Error Rate (WER) and timing metrics.

## Decision

A console application (Parlotype.Benchmark) with three-phase design using System.CommandLine + Spectre.Console, and hybrid JSON + SQLite storage.

**CLI Framework:**

- `System.CommandLine` for argument parsing: subcommands `run`, `list`, `compare`
- `Spectre.Console` for rich terminal output: progress bars, tables, colored diff
- Standard Unix-style CLI patterns: `--config`, `--datasets`, `--output`, `--tags`, `--samples`

**Dataset Structure:**

- JSON config files define benchmark suites with sample references and parameter overrides
- WAV files organized in datasets/ directory with metadata (reference transcription, tags, language)
- Tags (clean, noisy, short, long) allow filtered benchmark runs

**Three-Phase Pipeline:**

- `BenchmarkRunner` orchestrates: load config, iterate samples, run Whisper, compute WER, emit results
- WER calculation using Levenshtein distance at word level
- Timing captured via Stopwatch for each sample

**Hybrid Storage (JSON + SQLite):**

- **JSON files** in results/ directory: human-readable, git-diffable, portable. One file per run with full metadata (config, timestamps, per-sample results).
- **SQLite database** (results/benchmark.db): enables SQL queries across historical runs, aggregations, filtering by tag/model/date. Mirrors JSON data.
- Both formats written atomically on run completion

**Compare Command:**

- Takes two run IDs (--run-a, --run-b)
- Shows side-by-side WER and timing deltas per sample
- Color-coded: green for improvement, red for regression

## Consequences

- **Easier:** JSON results are version-controlled and diffable in PRs. SQLite enables ad-hoc analysis.
- **Easier:** Tag-based filtering allows quick smoke tests (clean+short) vs full suites.
- **Easier:** Compare command gives instant regression detection.
- **Harder:** Dual storage means writing results twice (JSON + SQLite). Schema changes need migration in both.
- **Harder:** Large benchmark suites with many samples produce substantial JSON files.
