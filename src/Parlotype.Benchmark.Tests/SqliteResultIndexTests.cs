using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class SqliteResultIndexTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteResultIndex _index;

    public SqliteResultIndexTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"parlotype-test-{Guid.NewGuid()}.db");
        _index = new SqliteResultIndex(_dbPath);
    }

    private static BenchmarkResult CreateResult(string runId = "test-run", string configName = "test-config",
        WhisperModelType model = WhisperModelType.Base, double? warmupTimeMs = null)
    {
        return new BenchmarkResult
        {
            RunId = runId,
            Timestamp = DateTimeOffset.UtcNow,
            Configuration = new BenchmarkConfig
            {
                Name = configName,
                Datasets = ["ds"],
                Whisper = new WhisperConfig { Model = model, Language = "en" },
                Vad = new VadConfig { Enabled = true },
            },
            Environment = new EnvironmentInfo(),
            Summary = new BenchmarkSummary
            {
                TotalSamples = 2,
                AverageWer = 10,
                AverageCer = 5,
                AverageRtf = 0.5,
                ModelLoadTimeMs = 500,
                WarmupTimeMs = warmupTimeMs,
                TotalProcessingTimeMs = 2000,
                PeakRamMb = 512,
            },
            Samples = [
                new SampleResult { Id = "s1", ReferenceText = "ref", HypothesisText = "hyp", Wer = 10, Cer = 5, ProcessingTimeMs = 1000, Rtf = 0.5 },
                new SampleResult { Id = "s2", ReferenceText = "ref2", HypothesisText = "hyp2", Wer = 10, Cer = 5, ProcessingTimeMs = 1000, Rtf = 0.5 },
            ],
        };
    }

    [Fact]
    public void Index_InsertsRun()
    {
        var result = CreateResult();
        _index.Index(result, "/path/to/result.json");

        Assert.Equal(1, _index.Count());
        Assert.True(_index.Contains("test-run"));
    }

    [Fact]
    public void Index_UpsertsSameRunId()
    {
        var result = CreateResult();
        _index.Index(result);
        _index.Index(result);

        Assert.Equal(1, _index.Count());
    }

    [Fact]
    public void ListRuns_ReturnsAllRuns()
    {
        _index.Index(CreateResult("run-1"));
        _index.Index(CreateResult("run-2"));

        var runs = _index.ListRuns();

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void ListRuns_FilterByModel()
    {
        _index.Index(CreateResult("run-base", model: WhisperModelType.Base));
        _index.Index(CreateResult("run-small", model: WhisperModelType.Small));

        var runs = _index.ListRuns(modelFilter: "Base");

        Assert.Single(runs);
        Assert.Equal("run-base", runs[0].RunId);
    }

    [Fact]
    public void ListRuns_FilterByConfig()
    {
        _index.Index(CreateResult("run-1", configName: "smoke"));
        _index.Index(CreateResult("run-2", configName: "full"));

        var runs = _index.ListRuns(configFilter: "smoke");

        Assert.Single(runs);
        Assert.Equal("run-1", runs[0].RunId);
    }

    [Fact]
    public void ListRuns_LimitResults()
    {
        _index.Index(CreateResult("run-1"));
        _index.Index(CreateResult("run-2"));
        _index.Index(CreateResult("run-3"));

        var runs = _index.ListRuns(limit: 2);

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void GetJsonPath_ReturnsStoredPath()
    {
        var result = CreateResult();
        _index.Index(result, "/path/to/result.json");

        Assert.Equal("/path/to/result.json", _index.GetJsonPath("test-run"));
    }

    [Fact]
    public void GetJsonPath_ReturnsNullForUnknownRun()
    {
        Assert.Null(_index.GetJsonPath("nonexistent"));
    }

    [Fact]
    public void Contains_ReturnsFalseForUnknownRun()
    {
        Assert.False(_index.Contains("nonexistent"));
    }

    [Fact]
    public void Index_PersistsWarmupTimeWhenSet()
    {
        _index.Index(CreateResult(warmupTimeMs: 1234.5));

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT warmup_ms FROM runs WHERE run_id = 'test-run'";
        var value = cmd.ExecuteScalar();
        Assert.Equal(1234.5, Convert.ToDouble(value));
    }

    [Fact]
    public void Index_PersistsNullWarmupTime()
    {
        _index.Index(CreateResult());

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT warmup_ms FROM runs WHERE run_id = 'test-run'";
        var value = cmd.ExecuteScalar();
        Assert.Equal(DBNull.Value, value);
    }

    [Fact]
    public void EnsureSchema_AddsWarmupColumnToLegacyDatabase()
    {
        // Simulate an older database by dropping the column after first creation, then
        // re-opening should idempotently re-add it.
        _index.Index(CreateResult("legacy-run"));
        _index.Dispose();

        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var drop = conn.CreateCommand();
            drop.CommandText = "ALTER TABLE runs DROP COLUMN warmup_ms";
            drop.ExecuteNonQuery();
        }

        using var migrated = new SqliteResultIndex(_dbPath);
        migrated.Index(CreateResult("new-run", warmupTimeMs: 42));

        using var conn2 = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        conn2.Open();
        using var cmd = conn2.CreateCommand();
        cmd.CommandText = "SELECT warmup_ms FROM runs WHERE run_id = 'new-run'";
        Assert.Equal(42.0, Convert.ToDouble(cmd.ExecuteScalar()));
    }

    public void Dispose()
    {
        _index.Dispose();
        try { File.Delete(_dbPath); } catch { /* best effort cleanup */ }
    }
}
