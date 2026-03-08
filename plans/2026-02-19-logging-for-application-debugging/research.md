# Task

I want to add logging to my Parlotype application. Research which .Net library is best today.

I'm interested in performance, low memory and CPU footprint, small binary size, console support (colors).

What did I miss? Use your judgement.

---

# Logging Library Research for Parlotype

## The Contenders

The serious options in 2025 for a performance-sensitive desktop app are **Microsoft.Extensions.Logging (MEL)**, **Serilog**, **NLog**, and **ZLogger**. Log4net is legacy — skip it.

---

## Criteria Breakdown

| Criterion | MEL (built-in) | Serilog | NLog | ZLogger |
|---|---|---|---|---|
| **Performance** | Medium | Medium-low | Medium-high | ⭐ Best |
| **Memory / Allocations** | Medium | Higher (boxing) | Medium | ⭐ Near-zero |
| **Binary size** | ⭐ Tiny (inbox) | Medium (core + sinks) | Medium | Small |
| **Colored console** | Basic | ⭐ Excellent (themes) | Good | Good (basic) |
| **Structured logging** | Via provider | ⭐ First-class | Good | Good |
| **Community / ecosystem** | ⭐ Huge | ⭐ Huge | Large | Small but growing |
| **Config via appsettings.json** | ✅ | ✅ | ✅ | ✅ |
| **Async/non-blocking writes** | Varies | Needs config | Needs config | ⭐ On by default |

---

## The Recommendation: **ZLogger v2** (on top of MEL abstraction)

For Parlotype specifically, ZLogger is the right call, and here's why it fits your use case better than the alternatives:

**Why ZLogger wins for Parlotype:**
- ZLogger is built directly on top of `Microsoft.Extensions.Logging`, eliminating the bridge overhead that Serilog and NLog require. Since you already use MEL-compatible DI in your Avalonia MVVM setup, there's zero friction — no dual-system overhead.
- ZLogger's allocation is overwhelmingly low compared to Serilog and NLog. This is critically important for Parlotype: GC pressure during audio recording/transcription can cause latency spikes and dropped audio frames. Zero-allocation logging eliminates this risk entirely.
- ZLogger is adjusted to be the fastest by default, and uses async-buffered writes automatically — you don't have to tune anything to get good behavior.
- It uses C# 10+ string interpolation natively, so your log calls look clean: `logger.ZLogInformation($"Transcription completed in {ms}ms")` with no boxing.
- It supports Console, File, RollingFile, InMemory, and Stream outputs — everything a desktop app needs, without pulling in 30 optional sink packages.

**Why not Serilog?** It's the most popular and has the most beautiful colored console output (with themes like `AnsiConsoleTheme`), but the quality of Serilog's bridge library to MEL is quite low, which is reflected in actual performance numbers. For a web app, this doesn't matter. For Parlotype, where the logger runs concurrent with audio capture and Whisper inference, it does.

**Why not NLog?** Solid and fast, but you'd be adding a third logging layer on top of MEL's abstraction with a somewhat dated API. No compelling advantage over ZLogger for your scenario.

---

## What You Missed (important gaps in your criteria)

**1. Async / non-blocking logging** — the most critical one for you. If your logger blocks on I/O (the default for most libraries!), it will compete with audio thread processing. ZLogger's async-buffered mode is on by default; others need explicit configuration or they'll flush synchronously on every write.

**2. NativeAOT / trimming compatibility** — Since you care about binary size, you likely care about AOT publishing. Serilog has known trimming issues; ZLogger and NLog handle it better. Worth verifying against your target .NET version.

**3. Log level filtering by category** — You'll want `Debug` for your VAD/audio pipeline and only `Information+` for UI components. All serious libraries support this via `appsettings.json`, but it's worth designing your category hierarchy upfront (e.g. `Parlotype.Audio`, `Parlotype.Transcription`, `Parlotype.UI`).

**4. Rolling file output** — A long-running voice app will accumulate logs. ZLogger's `RollingFile` with size or date-based rotation is built in; no extra packages needed.

**5. Release vs Debug behavior** — Consider stripping `Trace`/`Debug` logs entirely in Release builds using `[Conditional]` attributes or compile-time log level guards, so there's truly zero overhead in shipped builds.

**6. Colored console for development only** — This is relevant because colored console output has overhead (ANSI escape processing). The right pattern is: colored console in `Development`, plain or JSON console in `Release`, file rolling always.

---

## Bottom Line

Use **ZLogger v2** with the MEL abstraction you already have. Install:
```
dotnet add package ZLogger
```

Wire it up in your existing `IHostBuilder`/DI setup:
```csharp
logging.AddZLoggerConsole(options => {
    options.UsePlainTextFormatter(formatter => {
        formatter.SetPrefixFormatter($"{0} [{1}] ", (in MessageTemplate t, in LogInfo i) =>
            t.Format(i.Timestamp, i.LogLevel));
    });
});
logging.AddZLoggerRollingFile(options => {
    options.RollingInterval = RollingInterval.Day;
    options.FilePathSelector = (dt, seq) => $"logs/parlotype-{dt:yyyy-MM-dd}.log";
});
```

The zero-allocation story is the differentiator for Parlotype — every byte not allocated during transcription is a potential audio glitch avoided.