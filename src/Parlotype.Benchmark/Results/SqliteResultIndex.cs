using Microsoft.Data.Sqlite;

namespace Parlotype.Benchmark.Results;

/// <summary>SQLite-based index for querying historical benchmark runs without loading full JSON files.</summary>
public sealed class SqliteResultIndex : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteResultIndex(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS runs (
                run_id TEXT PRIMARY KEY,
                config_name TEXT NOT NULL,
                model TEXT NOT NULL,
                language TEXT NOT NULL,
                vad_enabled INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                avg_wer REAL NOT NULL,
                avg_cer REAL NOT NULL,
                avg_rtf REAL NOT NULL,
                model_load_ms REAL NOT NULL,
                warmup_ms REAL,
                total_time_ms REAL NOT NULL,
                peak_ram_mb REAL NOT NULL,
                total_samples INTEGER NOT NULL,
                json_path TEXT,
                os TEXT,
                architecture TEXT,
                dotnet_version TEXT,
                processor_count INTEGER,
                whisper_runtime TEXT
            );

            CREATE TABLE IF NOT EXISTS sample_results (
                run_id TEXT NOT NULL,
                sample_id TEXT NOT NULL,
                wer REAL NOT NULL,
                cer REAL NOT NULL,
                rtf REAL NOT NULL,
                processing_time_ms REAL NOT NULL,
                PRIMARY KEY (run_id, sample_id),
                FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_runs_timestamp ON runs(timestamp);
            CREATE INDEX IF NOT EXISTS idx_runs_config ON runs(config_name);
            CREATE INDEX IF NOT EXISTS idx_runs_model ON runs(model);
            """;
        cmd.ExecuteNonQuery();

        MigrateRunsAddColumnIfMissing("warmup_ms", "REAL");
    }

    /// <summary>Idempotent migration: adds a column to the runs table when missing.
    /// Uses <c>PRAGMA table_info</c> rather than catching <c>ALTER TABLE</c> errors so
    /// we don't swallow unrelated schema problems.</summary>
    private void MigrateRunsAddColumnIfMissing(string columnName, string columnType)
    {
        using (var checkCmd = _connection.CreateCommand())
        {
            checkCmd.CommandText = "PRAGMA table_info(runs)";
            using var reader = checkCmd.ExecuteReader();
            while (reader.Read())
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alterCmd = _connection.CreateCommand();
        alterCmd.CommandText = $"ALTER TABLE runs ADD COLUMN {columnName} {columnType}";
        alterCmd.ExecuteNonQuery();
    }

    /// <summary>Indexes a benchmark result into SQLite.</summary>
    public void Index(BenchmarkResult result, string? jsonPath = null)
    {
        using var transaction = _connection.BeginTransaction();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR REPLACE INTO runs
                (run_id, config_name, model, language, vad_enabled, timestamp,
                 avg_wer, avg_cer, avg_rtf, model_load_ms, warmup_ms, total_time_ms, peak_ram_mb, total_samples,
                 json_path, os, architecture, dotnet_version, processor_count, whisper_runtime)
                VALUES
                (@runId, @configName, @model, @language, @vadEnabled, @timestamp,
                 @avgWer, @avgCer, @avgRtf, @modelLoadMs, @warmupMs, @totalTimeMs, @peakRamMb, @totalSamples,
                 @jsonPath, @os, @arch, @dotnetVersion, @processorCount, @whisperRuntime)
                """;

            cmd.Parameters.AddWithValue("@runId", result.RunId);
            cmd.Parameters.AddWithValue("@configName", result.Configuration.Name);
            cmd.Parameters.AddWithValue("@model", result.Configuration.ModelDisplayName);
            cmd.Parameters.AddWithValue("@language", result.Configuration.EffectiveWhisper.Language);
            cmd.Parameters.AddWithValue("@vadEnabled", result.Configuration.Vad.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@timestamp", result.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("@avgWer", result.Summary.AverageWer);
            cmd.Parameters.AddWithValue("@avgCer", result.Summary.AverageCer);
            cmd.Parameters.AddWithValue("@avgRtf", result.Summary.AverageRtf);
            cmd.Parameters.AddWithValue("@modelLoadMs", result.Summary.ModelLoadTimeMs);
            cmd.Parameters.AddWithValue("@warmupMs", (object?)result.Summary.WarmupTimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalTimeMs", result.Summary.TotalProcessingTimeMs);
            cmd.Parameters.AddWithValue("@peakRamMb", result.Summary.PeakRamMb);
            cmd.Parameters.AddWithValue("@totalSamples", result.Summary.TotalSamples);
            cmd.Parameters.AddWithValue("@jsonPath", (object?)jsonPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@os", result.Environment.Os);
            cmd.Parameters.AddWithValue("@arch", result.Environment.Architecture);
            cmd.Parameters.AddWithValue("@dotnetVersion", result.Environment.DotnetVersion);
            cmd.Parameters.AddWithValue("@processorCount", result.Environment.ProcessorCount);
            cmd.Parameters.AddWithValue("@whisperRuntime", result.Environment.WhisperRuntime);

            cmd.ExecuteNonQuery();
        }

        // Delete existing sample results for this run before re-inserting
        using (var deleteCmd = _connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM sample_results WHERE run_id = @runId";
            deleteCmd.Parameters.AddWithValue("@runId", result.RunId);
            deleteCmd.ExecuteNonQuery();
        }

        using (var sampleCmd = _connection.CreateCommand())
        {
            sampleCmd.CommandText = """
                INSERT INTO sample_results (run_id, sample_id, wer, cer, rtf, processing_time_ms)
                VALUES (@runId, @sampleId, @wer, @cer, @rtf, @processingTimeMs)
                """;

            var runIdParam = sampleCmd.Parameters.Add("@runId", SqliteType.Text);
            var sampleIdParam = sampleCmd.Parameters.Add("@sampleId", SqliteType.Text);
            var werParam = sampleCmd.Parameters.Add("@wer", SqliteType.Real);
            var cerParam = sampleCmd.Parameters.Add("@cer", SqliteType.Real);
            var rtfParam = sampleCmd.Parameters.Add("@rtf", SqliteType.Real);
            var timeParam = sampleCmd.Parameters.Add("@processingTimeMs", SqliteType.Real);

            foreach (var sample in result.Samples)
            {
                runIdParam.Value = result.RunId;
                sampleIdParam.Value = sample.Id;
                werParam.Value = sample.Wer;
                cerParam.Value = sample.Cer;
                rtfParam.Value = sample.Rtf;
                timeParam.Value = sample.ProcessingTimeMs;
                sampleCmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    /// <summary>Lists benchmark runs with optional filtering.</summary>
    public List<RunSummaryRow> ListRuns(string? modelFilter = null, string? configFilter = null, int? limit = null)
    {
        using var cmd = _connection.CreateCommand();
        var sql = "SELECT run_id, config_name, model, timestamp, avg_wer, avg_cer, avg_rtf, total_samples, COALESCE(whisper_runtime, 'unknown') FROM runs WHERE 1=1";

        if (modelFilter is not null)
        {
            sql += " AND model = @model";
            cmd.Parameters.AddWithValue("@model", modelFilter);
        }

        if (configFilter is not null)
        {
            sql += " AND config_name = @config";
            cmd.Parameters.AddWithValue("@config", configFilter);
        }

        sql += " ORDER BY timestamp DESC";

        if (limit is not null)
        {
            sql += " LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit.Value);
        }

        cmd.CommandText = sql;

        var rows = new List<RunSummaryRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new RunSummaryRow(
                RunId: reader.GetString(0),
                ConfigName: reader.GetString(1),
                Model: reader.GetString(2),
                Timestamp: DateTimeOffset.Parse(reader.GetString(3)),
                AvgWer: reader.GetDouble(4),
                AvgCer: reader.GetDouble(5),
                AvgRtf: reader.GetDouble(6),
                TotalSamples: reader.GetInt32(7),
                Runtime: reader.IsDBNull(8) ? "unknown" : reader.GetString(8)));
        }

        return rows;
    }

    /// <summary>Gets the JSON file path for a run, if stored.</summary>
    public string? GetJsonPath(string runId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json_path FROM runs WHERE run_id = @runId";
        cmd.Parameters.AddWithValue("@runId", runId);
        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? null : (string)result;
    }

    /// <summary>Checks if a run is already indexed.</summary>
    public bool Contains(string runId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM runs WHERE run_id = @runId";
        cmd.Parameters.AddWithValue("@runId", runId);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    /// <summary>Returns the total number of indexed runs.</summary>
    public int Count()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM runs";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

/// <summary>Summary row from the runs table for list display.</summary>
public sealed record RunSummaryRow(
    string RunId,
    string ConfigName,
    string Model,
    DateTimeOffset Timestamp,
    double AvgWer,
    double AvgCer,
    double AvgRtf,
    int TotalSamples,
    string Runtime = "unknown");
