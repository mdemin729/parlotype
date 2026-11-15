using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Parlotype.Core.Feedback;

namespace Parlotype.Platform.Feedback;

/// <summary>
/// The one-line OS description the feedback dialog shows and may send (ADR-070):
/// <c>Windows 11 24H2 (10.0.26100)</c>.
/// </summary>
/// <remarks>
/// Windows reports 10.0 for both Windows 10 and 11; the marketing name is decided by the
/// build number (11 from 22000), and the feature-update name (<c>24H2</c>) is only in the
/// registry. macOS is reported by product version (<c>macOS 15.1</c>) rather than by its
/// Darwin kernel string. Other systems use <see cref="RuntimeInformation.OSDescription"/> as is.
/// </remarks>
internal static class OperatingSystemDescription
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    /// <summary>"macOS 13.7.8", or "macOS 15.1" when there is no patch component.</summary>
    internal static string FormatMacOs(Version version) =>
        version.Build > 0
            ? $"macOS {version.Major}.{version.Minor}.{version.Build}"
            : $"macOS {version.Major}.{version.Minor}";

    /// <summary>First Windows 11 build.</summary>
    private const int Windows11Build = 22000;

    public static string Current()
    {
        if (OperatingSystem.IsWindows())
            return FormatWindows(Environment.OSVersion.Version, ReadDisplayVersion());

        if (OperatingSystem.IsMacOS())
            return FeedbackLimits.NormalizeOperatingSystem(FormatMacOs(Environment.OSVersion.Version));

        return FeedbackLimits.NormalizeOperatingSystem(RuntimeInformation.OSDescription);
    }

    /// <summary>
    /// <c>Windows 11 24H2 (10.0.26100)</c> from a version and the registry's
    /// <c>DisplayVersion</c>, which may be missing on old builds.
    /// </summary>
    internal static string FormatWindows(Version version, string? displayVersion)
    {
        var name = version.Major == 10
            ? version.Build >= Windows11Build ? "Windows 11" : "Windows 10"
            : "Windows";

        var feature = string.IsNullOrWhiteSpace(displayVersion) ? "" : " " + displayVersion.Trim();
        var number = $"{version.Major}.{version.Minor}.{version.Build}";

        return FeedbackLimits.NormalizeOperatingSystem($"{name}{feature} ({number})");
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadDisplayVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);

            // DisplayVersion arrived with 20H2; ReleaseId is what older builds have.
            return key?.GetValue("DisplayVersion") as string
                ?? key?.GetValue("ReleaseId") as string;
        }
        catch (Exception)
        {
            // A description without "24H2" is still useful; never fail the dialog over it.
            return null;
        }
    }
}
