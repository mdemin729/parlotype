using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Feedback;
using Parlotype.Platform.MacOs;
using Parlotype.Platform.Settings;
using Parlotype.Platform.Speech;
using Parlotype.Platform.Startup;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// The platform-independent rules behind macOS support (ADR-077). Everything
/// here runs on any OS: the decisions take the platform as an argument, so the
/// macOS answer is checked on Windows CI and the Windows answer on a Mac.
/// </summary>
public class MacOsRulesTests
{
    // --- Default hotkeys ---

    [Fact]
    public void Mac_Defaults_Use_Right_Command_To_Talk_And_Have_No_Chord()
    {
        Assert.Equal(DictationHotkey.Hold(ModifierKey.Meta, ModifierSide.Right), DictationHotkeyDefaults.MacPushToTalk);
        Assert.Equal([DictationHotkeyDefaults.MacPushToTalk, DictationHotkeyDefaults.Toggle], DictationHotkeyDefaults.Mac);
        Assert.DoesNotContain(DictationHotkeyDefaults.Mac, b => b.Gesture.Kind == HotkeyGestureKind.Chord);
        Assert.All(DictationHotkeyDefaults.Mac, b => Assert.True(b.IsValid));
    }

    [Fact]
    public void Mac_Defaults_Do_Not_Conflict_With_Each_Other()
    {
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkeyDefaults.Toggle, [DictationHotkeyDefaults.MacPushToTalk]);

        Assert.Equal(HotkeyConflict.None, conflict);
    }

    [Fact]
    public void Defaults_For_The_Current_Platform_Follow_The_Operating_System()
    {
        var expected = OperatingSystem.IsMacOS() ? DictationHotkeyDefaults.Mac : DictationHotkeyDefaults.All;

        Assert.Equal(expected, DictationHotkeyDefaults.ForCurrentPlatform);
    }

    [Fact]
    public void Mac_Defaults_Survive_The_Settings_Codec()
    {
        var decoded = HotkeyBindingCodec.DecodeAll(HotkeyBindingCodec.EncodeAll(DictationHotkeyDefaults.Mac));

        Assert.Equal(DictationHotkeyDefaults.Mac, decoded);
    }

    // --- Which modifier is the command key ---

    [Fact]
    public void On_A_Mac_A_Key_Pressed_Late_In_A_Command_Hold_Aborts_It()
    {
        // Command is the shortcut key on macOS: Right Cmd+C during a hold is a
        // copy, not speech, however long the key has been down.
        var tracker = new ModifierHoldTracker(
            HotkeyGesture.ForHold(ModifierKey.Meta, ModifierSide.Right), deferStart: false, macModifierSemantics: true);

        Assert.Equal(HoldOutcome.Started, tracker.Process(
            HotkeyKeyEvent.ModifierDown(ModifierKey.Meta, ModifierSide.Right, 0)));
        Assert.Equal(HoldOutcome.Aborted,
            tracker.Process(HotkeyKeyEvent.KeyDown("C", 900, HotkeyModifiers.Meta)));
    }

    [Fact]
    public void On_A_Mac_Typing_Late_In_An_Option_Hold_Does_Not_Abort_It()
    {
        // Option composes characters on macOS (Option+E, Option+U), like Shift.
        var tracker = new ModifierHoldTracker(
            HotkeyGesture.ForHold(ModifierKey.Alt, ModifierSide.Right), deferStart: false, macModifierSemantics: true);

        Assert.Equal(HoldOutcome.Started, tracker.Process(
            HotkeyKeyEvent.ModifierDown(ModifierKey.Alt, ModifierSide.Right, 0)));
        Assert.Equal(HoldOutcome.None,
            tracker.Process(HotkeyKeyEvent.KeyDown("E", 900, HotkeyModifiers.Alt)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(
            HotkeyKeyEvent.ModifierUp(ModifierKey.Alt, ModifierSide.Right, 3000)));
    }

    [Fact]
    public void Off_A_Mac_A_Meta_Hold_Keeps_The_Grace_Window()
    {
        // The pre-existing rule, pinned so the macOS branch cannot leak into it.
        var tracker = new ModifierHoldTracker(
            HotkeyGesture.ForHold(ModifierKey.Meta, ModifierSide.Right), deferStart: false);

        Assert.Equal(HoldOutcome.Started, tracker.Process(
            HotkeyKeyEvent.ModifierDown(ModifierKey.Meta, ModifierSide.Right, 0)));
        Assert.Equal(HoldOutcome.None,
            tracker.Process(HotkeyKeyEvent.KeyDown("C", 900, HotkeyModifiers.Meta)));
    }

    [Fact]
    public void The_Matcher_Passes_Mac_Semantics_To_Its_Holds()
    {
        var matcher = new HotkeyGestureMatcher(DictationHotkeyDefaults.Mac, macModifierSemantics: true);

        // Double-tap Ctrl does not share Right Command's key, so the hold starts at once.
        Assert.Equal(DictationAction.Start, matcher.Process(
            HotkeyKeyEvent.ModifierDown(ModifierKey.Meta, ModifierSide.Right, 0, HotkeyModifiers.Meta)).Action);
        Assert.Equal(DictationAction.Cancel,
            matcher.Process(HotkeyKeyEvent.KeyDown("V", 1200, HotkeyModifiers.Meta)).Action);
    }

    // --- Default engine ---

    [Fact]
    public void The_Default_Engine_Is_Parakeet_Where_It_Can_Run_And_Whisper_Where_It_Cannot()
    {
        Assert.Equal(SpeechEngine.Parakeet, SpeechEngineDefaults.For(parakeetAvailable: true));
        Assert.Equal(SpeechEngine.Whisper, SpeechEngineDefaults.For(parakeetAvailable: false));
        Assert.Equal(SpeechEngineDefaults.For(SpeechEngineDefaults.IsParakeetAvailable), SpeechEngineDefaults.Default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NotARealEngine")]
    public void An_Unset_Or_Unparsable_Engine_Is_The_Default(string? stored)
    {
        Assert.Equal(SpeechEngineDefaults.Default, SpeechEngineDefaults.Parse(stored));
    }

    [Theory]
    [InlineData("Whisper", SpeechEngine.Whisper)]
    [InlineData("gemma4", SpeechEngine.Gemma4)]
    [InlineData("Parakeet", SpeechEngine.Parakeet)]
    public void A_Stored_Engine_Is_Honoured_Whatever_The_Default(string stored, SpeechEngine expected)
    {
        Assert.Equal(expected, SpeechEngineDefaults.Parse(stored));
    }

    [Fact]
    public void Parakeet_Is_Available_Everywhere_But_Old_MacOs()
    {
        var expected = !OperatingSystem.IsMacOS() || OperatingSystem.IsMacOSVersionAtLeast(14, 4);

        Assert.Equal(expected, SpeechEngineDefaults.IsParakeetAvailable);
    }

    // --- ONNX Runtime fallback ---

    [Theory]
    [InlineData(true, Architecture.X64, false, true)]    // Intel Mac, old macOS: the one case
    [InlineData(true, Architecture.X64, true, false)]    // Intel Mac, current macOS
    [InlineData(true, Architecture.Arm64, false, false)] // no fallback is shipped for Apple Silicon
    [InlineData(false, Architecture.X64, false, false)]  // not a Mac
    public void The_Fallback_Onnx_Runtime_Is_Used_Only_On_Intel_Macs_That_Cannot_Load_The_Bundled_One(
        bool isMacOs, Architecture architecture, bool bundledRuntimeLoads, bool expected)
    {
        Assert.Equal(expected, LegacyOnnxRuntime.IsNeeded(isMacOs, architecture, bundledRuntimeLoads));
    }

    [Fact]
    public void On_A_Mac_Build_The_Fallback_Onnx_Runtime_Is_Next_To_The_Tests()
    {
        // The csproj copies it only when building on macOS for Intel or for no
        // particular runtime; a build that lost that item would leave an old
        // Intel Mac without voice activity detection.
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return;

        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, LegacyOnnxRuntime.RelativePath)));
    }

    // --- Launch agent target ---

    [Theory]
    [InlineData("/Applications/Parlotype.app/Contents/MacOS/Parlotype", true)]
    [InlineData("/Users/me/Applications/Parlotype.app/Contents/MacOS/Parlotype", true)]
    [InlineData("/Users/me/Downloads/Parlotype.app/Contents/MacOS/Parlotype", false)]
    [InlineData("/Volumes/Parlotype/Parlotype.app/Contents/MacOS/Parlotype", false)]
    [InlineData("/Users/me/repo/src/Parlotype.Desktop/bin/Debug/net10.0/Parlotype", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_A_Copy_In_An_Applications_Folder_Registers_For_Login(string? processPath, bool registers)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var target = MacOsLaunchAgentLaunchAtLoginService.ResolveInstalledExecutable(processPath, "/Users/me");

        Assert.Equal(registers ? processPath : null, target);
    }

    // --- Feedback OS description ---

    [Theory]
    [InlineData(13, 7, 8, "macOS 13.7.8")]
    [InlineData(15, 1, 0, "macOS 15.1")]
    public void MacOs_Is_Described_By_Product_Version(int major, int minor, int build, string expected)
    {
        Assert.Equal(expected, OperatingSystemDescription.FormatMacOs(new Version(major, minor, build)));
    }

    // --- Permissions off macOS ---

    [Fact]
    public async Task Without_A_Privacy_Gate_Every_Permission_Reads_As_Granted()
    {
        var permissions = new NoOpSystemPermissionService();

        Assert.Equal(SystemPermissionState.Granted, permissions.GetState(SystemPermission.Accessibility));
        Assert.Equal(SystemPermissionState.Granted, permissions.GetState(SystemPermission.Microphone));
        Assert.Equal(SystemPermissionState.Granted, await permissions.RequestAsync(SystemPermission.Microphone));
        Assert.False(permissions.OpenSettings(SystemPermission.Accessibility));
    }
}

/// <summary>
/// Exercises the real macOS interop (ADR-077). macOS-only; every test no-ops
/// elsewhere, matching <c>WindowsRunKeyLaunchAtLoginServiceTests</c>. Nothing
/// here needs a privacy grant or shows a prompt: no microphone is opened and no
/// key is posted.
/// <para>
/// CoreAudio is deliberately absent. Its first call in a process initialises
/// the audio hardware layer, and inside the test host that initialisation can
/// block forever in the built-in audio driver
/// (<c>docs/knowledge/coreaudio-first-call-can-block.md</c>). Device listing
/// and capture are verified by running the app.
/// </para>
/// </summary>
public class MacOsInteropTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "parlotype-tests-" + Guid.NewGuid().ToString("N"));

    private const string FakeExecutable = "/Applications/Parlotype.app/Contents/MacOS/Parlotype";

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    private MacOsLaunchAgentLaunchAtLoginService BuildLoginService(string? target = FakeExecutable)
    {
        // Guarded by the OperatingSystem.IsMacOS() early-return in each test;
        // the type is [SupportedOSPlatform("macos")].
        if (!OperatingSystem.IsMacOS())
            throw new InvalidOperationException("macOS-only");

        return new MacOsLaunchAgentLaunchAtLoginService(
            NullLogger<MacOsLaunchAgentLaunchAtLoginService>.Instance, () => target, _scratch);
    }

    /// <summary>Same value as <c>MacOsLaunchAgentLaunchAtLoginService.Label</c>, which is macOS-only to the analyzer.</summary>
    private const string AgentLabel = "dev.parlotype.Parlotype";

    private string AgentFile => Path.Combine(_scratch, AgentLabel + ".plist");

    [Fact]
    public void LaunchAgent_WithNothingRegistered_IsDisabled()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        Assert.Equal(LaunchAtLoginState.Disabled, BuildLoginService().GetState());
    }

    [Fact]
    public void LaunchAgent_Enable_WritesAPropertyList_AndReadsBackAsEnabled()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var service = BuildLoginService();

        Assert.True(service.SetEnabled(true));
        Assert.Equal(LaunchAtLoginState.Enabled, service.GetState());

        var plist = File.ReadAllText(AgentFile);
        Assert.Contains($"<string>{FakeExecutable}</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>", plist);
        Assert.Equal(AgentLabel, MacOsLaunchAgentLaunchAtLoginService.Label);
        Assert.Contains($"<string>{AgentLabel}</string>", plist);
    }

    [Fact]
    public void LaunchAgent_Disable_RemovesTheFile_AndIsIdempotent()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var service = BuildLoginService();
        service.SetEnabled(true);

        Assert.True(service.SetEnabled(false));
        Assert.False(File.Exists(AgentFile));
        Assert.Equal(LaunchAtLoginState.Disabled, service.GetState());
        Assert.True(service.SetEnabled(false));
    }

    [Fact]
    public void LaunchAgent_PointingAtAnotherCopy_IsNotAWorkingRegistration()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        BuildLoginService("/Applications/Old/Parlotype.app/Contents/MacOS/Parlotype").SetEnabled(true);

        Assert.Equal(LaunchAtLoginState.Disabled, BuildLoginService().GetState());
    }

    [Fact]
    public void LaunchAgent_ForACopyThatCannotRegister_IsUnsupportedAndWritesNothing()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var service = BuildLoginService(target: null);

        Assert.False(service.IsSupported);
        Assert.Equal(LaunchAtLoginState.Unsupported, service.GetState());
        Assert.False(service.SetEnabled(true));
        Assert.False(File.Exists(AgentFile));
    }

    [Fact]
    public void LaunchAgent_EscapesAPathThatIsNotPlainXml()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        const string awkward = "/Applications/Tom & Jerry/Parlotype.app/Contents/MacOS/Parlotype";
        var service = BuildLoginService(awkward);

        Assert.True(service.SetEnabled(true));
        Assert.Contains("Tom &amp; Jerry", File.ReadAllText(AgentFile));
        Assert.Equal(LaunchAtLoginState.Enabled, service.GetState());
    }

    [Fact]
    public async Task Keychain_RoundTripsOverwritesAndDeletesASecret()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // A throwaway service, so the user's real "Parlotype" items are never touched.
        var store = new KeychainSecretStore(
            "Parlotype.Tests." + Guid.NewGuid().ToString("N"), NullLogger<KeychainSecretStore>.Instance);
        const string key = "ApiKey";

        try
        {
            Assert.Null(await store.GetAsync(key));

            await store.SetAsync(key, "sk-first-ключ");
            Assert.Equal("sk-first-ключ", await store.GetAsync(key));

            await store.SetAsync(key, "sk-second");
            Assert.Equal("sk-second", await store.GetAsync(key));

            await store.SetAsync(key, null);
            Assert.Null(await store.GetAsync(key));

            // Removing what is not there is not an error.
            await store.SetAsync(key, string.Empty);
        }
        finally
        {
            await store.SetAsync(key, null);
        }
    }

    [Fact]
    public async Task KeyboardLayout_OffTheMainThread_AnswersOrGivesUp_ButNeverHangsOrThrows()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // A test host has no run loop draining the main queue, so this takes
        // the timeout path; in the app it takes the dispatch path.
        var layout = Task.Run(DetectKeyboardLayout);

        Assert.Same(layout, await Task.WhenAny(layout, Task.Delay(TimeSpan.FromSeconds(5))));
        if (await layout is { } info)
            Assert.Equal(2, info.LanguageCode.Length);
    }

    private static KeyboardLayoutInfo? DetectKeyboardLayout() =>
        OperatingSystem.IsMacOS() ? new MacOsKeyboardLayoutService().Detect() : null;

    [Fact]
    public void Permissions_CanBeReadWithoutPrompting()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var permissions = new MacOsSystemPermissionService(NullLogger<MacOsSystemPermissionService>.Instance);

        Assert.True(Enum.IsDefined(permissions.GetState(SystemPermission.Accessibility)));
        Assert.True(Enum.IsDefined(permissions.GetState(SystemPermission.Microphone)));
    }

    /// <summary>
    /// The microphone request hands AVFoundation an Objective-C block built by
    /// hand. A wrong field in it is a crash on a user's first dictation, in a
    /// state a developer's machine is rarely in, so the block is exercised here
    /// through libdispatch instead.
    /// </summary>
    [Fact]
    public async Task TheHandBuiltCompletionBlock_IsCallableByTheSystem()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var invoked = InvokeCompletionBlock();

        Assert.Same(invoked, await Task.WhenAny(invoked, Task.Delay(TimeSpan.FromSeconds(5))));
    }

    private static Task InvokeCompletionBlock() =>
        OperatingSystem.IsMacOS() ? MacOsPrivacy.InvokeCompletionBlockForTestAsync() : Task.CompletedTask;

    [Fact]
    public void Strings_SurviveTheRoundTripThroughCoreFoundation()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        foreach (var text in new[] { "", "plain", "Привет, мир", "emoji 🎙️ and ✕" })
        {
            var native = MacOsNative.CreateString(text);
            try
            {
                Assert.Equal(text, MacOsNative.ReadString(native));
            }
            finally
            {
                MacOsNative.CFRelease(native);
            }
        }
    }
}
