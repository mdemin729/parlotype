using Parlotype.Core.Settings;
using Velopack.Logging;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Minimal <see cref="IVelopackLogger"/> that appends to <c>velopack.log</c> in the
/// app's log directory.
/// </summary>
/// <remarks>
/// <para>
/// Velopack runs before DI and ZLogger exist — <c>VelopackApp.Build().Run()</c> is
/// the first statement in <see cref="Program.Main"/> (ADR-053) — so the install,
/// update and uninstall hooks cannot use the app's normal logger. Without this,
/// failures in those hooks are invisible: they happen in a short-lived process
/// that the user never sees.
/// </para>
/// <para>
/// Writes to the data directory, not the pack folder, so the log survives the
/// uninstall it is recording. Every operation is best-effort: a logger that
/// throws during an install hook would fail the install.
/// </para>
/// </remarks>
internal sealed class VelopackFileLogger : IVelopackLogger
{
    private static readonly Lock Gate = new();

    public static VelopackFileLogger Instance { get; } = new();

    private VelopackFileLogger()
    {
    }

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        try
        {
            var directory = AppPaths.Default.LogsDirectory;
            Directory.CreateDirectory(directory);

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {message}"
                + (exception is null ? string.Empty : $"{Environment.NewLine}{exception}");

            lock (Gate)
            {
                File.AppendAllText(Path.Combine(directory, "velopack.log"), line + Environment.NewLine);
            }
        }
        catch
        {
            // Never let logging break an install, update or uninstall hook.
        }
    }
}
