using BenchmarkDotNet.Running;

// Micro-benchmarks for audio-pipeline hot paths (see
// plans/2026-07-11-audio-pipeline-perf-security/). Run in Release:
//
//   dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter *
//
// Allocation columns (MemoryDiagnoser) are the primary signal; wall-clock
// times are secondary and machine-dependent.
BenchmarkSwitcher
    .FromAssembly(typeof(Parlotype.MicroBenchmarks.WavEncoderBenchmarks).Assembly)
    .Run(args);
