using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Parlotype.Platform.Startup;

/// <summary>
/// Reads the id of the process that created the current one (ADR-062).
/// </summary>
/// <remarks>
/// <para>
/// .NET exposes no managed way to get a parent process id, so this goes through
/// <c>ntdll!NtQueryInformationProcess</c> with <c>ProcessBasicInformation</c> and
/// reads <c>InheritedFromUniqueProcessId</c>. That field records the <em>creator</em>
/// at spawn time; Windows never updates it, so a resolved id can point at a
/// process that has since exited (or, after a long time, at a reused id) — callers
/// must treat it as a hint and verify the process is alive.
/// </para>
/// <para>
/// Windows-only. Every other platform returns <see langword="false"/>: the one
/// caller (<c>ParentProcessExitWatcher</c>) only needs this to catch orphaned
/// <c>dotnet run</c> children on Windows, and Unix shells propagate termination
/// to the child anyway.
/// </para>
/// </remarks>
public static class NativeParentProcess
{
    /// <summary>
    /// Resolves the parent (creator) process id of the current process.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> with a positive <paramref name="parentProcessId"/> on
    /// success; <see langword="false"/> on any failure, a non-positive id, or a
    /// non-Windows OS.
    /// </returns>
    public static bool TryGetParentProcessId(out int parentProcessId)
    {
        parentProcessId = 0;
        return OperatingSystem.IsWindows() && TryGetParentProcessIdWindows(out parentProcessId);
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetParentProcessIdWindows(out int parentProcessId)
    {
        parentProcessId = 0;

        try
        {
            var info = default(ProcessBasicInformation);
            var status = NtQueryInformationProcess(
                GetCurrentProcess(),
                processInformationClass: 0, // ProcessBasicInformation
                ref info,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);

            if (status != 0)
                return false;

            var pid = info.InheritedFromUniqueProcessId.ToInt64();
            if (pid is <= 0 or > int.MaxValue)
                return false;

            parentProcessId = (int)pid;
            return true;
        }
        catch (Exception)
        {
            // A missing entry point or a hardened process (unlikely on a desktop
            // Windows box) just means "cannot tell" — never a crash.
            return false;
        }
    }

    /// <summary>
    /// Layout matches the native <c>PROCESS_BASIC_INFORMATION</c>. Every field is
    /// pointer-sized here, including the two that are really 32-bit
    /// (<c>ExitStatus</c>, <c>BasePriority</c>): on a 64-bit process the natural
    /// alignment padding after them is exactly the extra 4 bytes, so declaring
    /// them <see cref="nint"/> keeps the following pointers at the right offsets.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        nint processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();
}
