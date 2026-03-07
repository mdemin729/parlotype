# Phase 2 Implementation Plan — Comparison & Storage

## Problem Statement

Phase 1 delivered a working benchmark MVP that runs Whisper against WAV/FLAC datasets, computes WER/CER/RTF, and saves JSON results. However, there is no way to query historical runs, compare configurations, or export results in formats other than JSON. Phase 2 adds a SQLite index for historical querying, `list` and `compare` CLI commands, CSV/Markdown export, and tag-based sample filtering.

## Scope

**In scope (Phase 2):**
- SQLite index (`benchmarks.db`) for historical runs, populated automatically after each `run`
- `import` command — rebuild SQLite index from existing JSON result files
- `list` command — query and display historical runs with optional filters
- `compare` command — side-by-side comparison of two runs with delta metrics
- `export` command — export a single run's results as CSV or Markdown
- Comparison output in multiple formats (console table, JSON, CSV, Markdown)
- Tag-based sample filtering in `run` command (`--tags clean,short`)
- Sample ID filtering in `run` command (`--samples kennedy,one-small-step`)

**Out of scope (deferred to Phase 3+):**
- RAM/VRAM measurement improvements
- Parameter sweep automation
- Result stability analysis
- CI/CD regression detection
- Grafana integration

## Phase 1 Baseline (What Exists)

| Component | Location | Status |
|-----------|----------|--------|
| CLI with `run` command | `Program.cs` | ✅ System.CommandLine beta5 |
| JSON result storage | `Results/JsonResultStore.cs` | ✅ Save/Load |
| Console summary table | `Reporting/ConsoleReporter.cs` | ✅ Spectre.Console |
| Data models | `Configuration/`, `Results/` | ✅ BenchmarkConfig, BenchmarkResult, SampleResult |
| Benchmark runner | `Pipeline/BenchmarkRunner.cs` | ✅ Orchestration with VAD toggle |
| Audio loader | `Pipeline/AudioFileLoader.cs` | ✅ WAV + FLAC (via FFmpeg) |
| Metrics | `Metrics/` | ✅ WER/CER + text normalization |
| Unit tests | `Parlotype.Benchmark.Tests/` | ✅ 22 tests |

---

## New Project Structure

```
src/Parlotype.Benchmark/
  ...existing Phase 1 files...
  Storage/
    SqliteResultIndex.cs          # SQLite index: create/populate/query
  Reporting/
    ConsoleReporter.cs            # (existing) — add DisplayComparison(), DisplayRunList()
    CsvFormatter.cs               # Export results as CSV
    MarkdownFormatter.cs          # Export results as Markdown table
    ComparisonResult.cs           # Data model for run-vs-run comparison
    ResultComparer.cs             # Compute deltas between two BenchmarkResults
```

---

## Todos

### 1. `add-sqlite-package` — Add Microsoft.Data.Sqlite NuGet reference

Add `<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.2" />` to `Parlotype.Benchmark.csproj`. Verify build succeeds.

Note: Use the latest stable 9.x release (not the 10.0.0-preview) to avoid preview dependency issues. Check latest version at implementation time.

### 2. `implement-sqlite-index` — SQLite index for historical runs

Create `src/Parlotype.Benchmark/Storage/SqliteResultIndex.cs`:

```csharp
public sealed class SqliteResultIndex : IDisposable
{
    public SqliteResultIndex(string dbPath);

    // Create tables if they don't exist
    void EnsureSchema();

    // Insert a BenchmarkResult into the index
    void IndexResult(BenchmarkResult result, string jsonPath);

    // Query runs with optional filters
    List<RunSummaryRow> ListRuns(string? model = null, int? limit = null);

    // Get a single run by ID
    RunSummaryRow? GetRun(string runId);

    // Get sample results for a run
    List<SampleResultRow> GetSampleResults(string runId);

    // Delete a run from the index
    void DeleteRun(string runId);
}
```

SQLite schema (from requirements):
```sql
CREATE TABLE IF NOT EXISTS runs (
    run_id TEXT PRIMARY KEY,
    timestamp TEXT NOT NULL,
    config_name TEXT,
    model TEXT,
    device TEXT,
    vad_enabled INTEGER,
    avg_wer REAL,
    avg_cer REAL,
    avg_rtf REAL,
    model_load_time_ms REAL,
    peak_ram_mb REAL,
    total_processing_time_ms REAL,
    total_samples INTEGER,
    json_path TEXT
);

CREATE TABLE IF NOT EXISTS sample_results (
    run_id TEXT REFERENCES runs(run_id) ON DELETE CASCADE,
    sample_id TEXT,
    wer REAL,
    cer REAL,
    processing_time_ms REAL,
    rtf REAL,
    PRIMARY KEY (run_id, sample_id)
);
```

Define `RunSummaryRow` and `SampleResultRow` as lightweight records for query results (not the full JSON-rich models).

### 3. `auto-index-after-run` — Index results into SQLite after each benchmark run

Modify the `run` command handler in `Program.cs`:
- After `JsonResultStore.SaveAsync()`, open/create `benchmarks.db` in the output directory
- Call `SqliteResultIndex.IndexResult(result, savedPath)`
- Log: "Indexed in benchmarks.db"

### 4. `implement-import-command` — Rebuild SQLite index from JSON files

Add `import` command to `Program.cs`:

```
parlotype-bench import --results <results-dir>
```

- Scan the results directory for `*.json` files
- Deserialize each via `JsonResultStore.LoadAsync()`
- Insert into SQLite index (skip duplicates by run_id)
- Display count of imported/skipped runs

### 5. `implement-list-command` — Query and display historical runs

Add `list` command to `Program.cs`:

```
parlotype-bench list --results <results-dir> [--model Base] [--last 10]
```

Options:
- `--results` (optional, default `./results`) — directory containing `benchmarks.db`
- `--model` — filter by Whisper model name
- `--last` — show only the N most recent runs

Display as a Spectre.Console table:
```
┌────────────────────────────────────┬───────┬───────┬───────┬───────────┐
│ Run ID                             │ Model │ WER % │ CER % │ RTF       │
├────────────────────────────────────┼───────┼───────┼───────┼───────────┤
│ 20260305-012100-smoke-test-base... │ Base  │ 8.5   │ 3.1   │ 0.250     │
│ 20260305-013000-smoke-test-smal... │ Small │ 5.2   │ 2.0   │ 0.550     │
└────────────────────────────────────┴───────┴───────┴───────┴───────────┘
```

### 6. `implement-result-comparer` — Compute deltas between two runs

Create `src/Parlotype.Benchmark/Reporting/ComparisonResult.cs`:

```csharp
public sealed record ComparisonResult
{
    public required BenchmarkResult RunA { get; init; }
    public required BenchmarkResult RunB { get; init; }
    public required MetricDelta WerDelta { get; init; }
    public required MetricDelta CerDelta { get; init; }
    public required MetricDelta RtfDelta { get; init; }
    public required MetricDelta ModelLoadDelta { get; init; }
    public required MetricDelta PeakRamDelta { get; init; }
    public required MetricDelta TotalTimeDelta { get; init; }
    public List<SampleComparisonRow> SampleDeltas { get; init; } = [];
}

public sealed record MetricDelta(double ValueA, double ValueB, bool LowerIsBetter = true)
{
    public double Absolute => ValueB - ValueA;
    public double? Relative => ValueA != 0 ? (ValueB - ValueA) / ValueA * 100 : null;
    public bool IsImproved => LowerIsBetter ? ValueB < ValueA : ValueB > ValueA;
}

public sealed record SampleComparisonRow(string SampleId, double WerA, double WerB, double CerA, double CerB);
```

Create `src/Parlotype.Benchmark/Reporting/ResultComparer.cs`:

```csharp
public static class ResultComparer
{
    public static ComparisonResult Compare(BenchmarkResult runA, BenchmarkResult runB);
}
```

Match samples by ID across runs. Compute deltas for all summary metrics. Flag per-sample regressions.

### 7. `implement-compare-command` — Side-by-side comparison CLI

Add `compare` command to `Program.cs`:

```
parlotype-bench compare <run-id-a> <run-id-b> --results <results-dir> [--format console|json|csv|md]
```

Arguments:
- `run-id-a`, `run-id-b` — run IDs to compare (positional arguments)
- `--results` (optional, default `./results`) — directory containing results
- `--format` (optional, default `console`) — output format

Workflow:
1. Open SQLite index to resolve run-id → json_path
2. Load both `BenchmarkResult` via `JsonResultStore.LoadAsync()`
3. Compute `ComparisonResult` via `ResultComparer.Compare()`
4. Format and output based on `--format`

### 8. `implement-comparison-display` — Console comparison table

Add `ConsoleReporter.DisplayComparison(ComparisonResult)`:

```
┌──────────────────┬────────────┬────────────┬──────────┐
│ Metric           │ Run A      │ Run B      │ Δ        │
├──────────────────┼────────────┼────────────┼──────────┤
│ Model            │ Base       │ Small      │ -        │
│ Avg WER          │ 8.5%       │ 5.2%       │ -3.3% ✅ │
│ Avg CER          │ 3.1%       │ 2.0%       │ -1.1% ✅ │
│ Avg RTF          │ 0.25       │ 0.55       │ +0.30 ⚠️ │
│ Model Load (ms)  │ 450        │ 1200       │ +750  ⚠️ │
│ Peak RAM (MB)    │ 800        │ 1500       │ +700  ⚠️ │
└──────────────────┴────────────┴────────────┴──────────┘
```

Use ✅ for improvements, ⚠️ for regressions. Color-code cells green/red.

Optionally display per-sample delta table if samples overlap.

### 9. `implement-csv-formatter` — CSV export

Create `src/Parlotype.Benchmark/Reporting/CsvFormatter.cs`:

```csharp
public static class CsvFormatter
{
    // Export a single run's results as CSV
    public static string FormatResult(BenchmarkResult result);

    // Export a comparison as CSV
    public static string FormatComparison(ComparisonResult comparison);
}
```

CSV columns for single run: `SampleId,ReferenceText,HypothesisText,WER,CER,RTF,ProcessingTimeMs`

CSV columns for comparison: `SampleId,WER_A,WER_B,WER_Delta,CER_A,CER_B,CER_Delta`

### 10. `implement-markdown-formatter` — Markdown export

Create `src/Parlotype.Benchmark/Reporting/MarkdownFormatter.cs`:

```csharp
public static class MarkdownFormatter
{
    // Export a single run as markdown
    public static string FormatResult(BenchmarkResult result);

    // Export a comparison as markdown
    public static string FormatComparison(ComparisonResult comparison);
}
```

Produce markdown tables compatible with GitHub/GitLab rendering.

### 11. `implement-export-command` — Export a single run's results

Add `export` command to `Program.cs`:

```
parlotype-bench export <run-id> --results <results-dir> --format csv|md|json
```

- Resolve run-id via SQLite index → load JSON
- Format via the appropriate formatter
- Output to stdout (pipeable to file)

### 12. `implement-tag-filtering` — Filter samples by tags during run

Add `--tags` option to the `run` command:

```
parlotype-bench run --config ... --datasets ... --tags clean,short
```

- Parse comma-separated tag list
- In `BenchmarkRunner.RunAsync()`, filter `allSamples` to only include samples where `SampleInfo.Tags` contains ALL specified tags (AND logic)
- Log how many samples were filtered

Also add `--samples` option for filtering by sample ID:

```
parlotype-bench run --config ... --datasets ... --samples kennedy,one-small-step
```

### 13. `implement-phase2-tests` — Unit tests for new functionality

Add tests to `src/Parlotype.Benchmark.Tests/`:

**`SqliteResultIndexTests.cs`:**
- Creates database and schema
- Indexes a result and retrieves it
- Lists runs with filters
- Handles duplicate run_id gracefully

**`ResultComparerTests.cs`:**
- Computes correct deltas for known inputs
- Handles mismatched sample sets
- Correctly identifies improvements vs regressions

**`CsvFormatterTests.cs`:**
- Produces valid CSV with headers
- Escapes commas and quotes in text fields

**`MarkdownFormatterTests.cs`:**
- Produces valid markdown table with alignment

### 14. `verify-phase2` — Build, test, and smoke-run all new commands

- `dotnet build Parlotype.slnx` — zero warnings
- `dotnet test` — all tests pass
- Smoke-test each new command:
  - `run` with `--tags` filter
  - `import --results results`
  - `list --results results`
  - `compare <run-a> <run-b> --results results`
  - `compare <run-a> <run-b> --format md`
  - `export <run-id> --format csv`

---

## Dependencies Between Todos

```
add-sqlite-package
  └── implement-sqlite-index
        ├── auto-index-after-run
        ├── implement-import-command
        ├── implement-list-command
        └── implement-compare-command

implement-result-comparer (independent of SQLite)
  ├── implement-compare-command
  └── implement-comparison-display

implement-csv-formatter (independent)
implement-markdown-formatter (independent)
  └── implement-export-command
        depends on: implement-sqlite-index (for run-id resolution)

implement-tag-filtering (independent of other Phase 2 work)

implement-compare-command
  depends on: implement-sqlite-index, implement-result-comparer

implement-comparison-display
  depends on: implement-result-comparer

implement-export-command
  depends on: implement-sqlite-index, implement-csv-formatter, implement-markdown-formatter

implement-phase2-tests
  depends on: implement-sqlite-index, implement-result-comparer, implement-csv-formatter, implement-markdown-formatter

verify-phase2
  depends on: all above
```

### Parallelization Opportunities

**Wave 1 (no dependencies):**
- `add-sqlite-package`
- `implement-result-comparer` + `implement-comparison-display`
- `implement-csv-formatter`
- `implement-markdown-formatter`
- `implement-tag-filtering`

**Wave 2 (depends on Wave 1):**
- `implement-sqlite-index` (depends on sqlite package)
- `implement-export-command` (depends on sqlite + formatters)

**Wave 3 (depends on Wave 2):**
- `auto-index-after-run`
- `implement-import-command`
- `implement-list-command`
- `implement-compare-command`

**Wave 4:**
- `implement-phase2-tests`
- `verify-phase2`

---

## Notes

- The `benchmarks.db` file lives in the results directory (same as JSON files). It can be deleted and rebuilt via `import`.
- Run IDs are used as primary keys. The `compare` command accepts partial run-id prefixes for convenience (match via `LIKE '{prefix}%'`).
- CSV output uses `"` quoting for fields containing commas, following RFC 4180.
- Markdown tables use GitHub-flavored markdown with pipe separators and alignment.
- Tag filtering uses AND logic (all specified tags must be present). This matches the common use case of narrowing down to "clean AND short" samples.
- The `--format` option for `compare` defaults to `console`. When a non-console format is used, output goes to stdout for piping (e.g., `compare A B --format csv > comparison.csv`).
