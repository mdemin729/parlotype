using Parlotype.Core.LlamaServer;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>Row view-model for an installable catalog variant in the Available list.</summary>
public sealed class LlamaServerVariantRowVm
{
    public LlamaServerVariant Variant { get; }
    public string Build => Variant.Build;
    public LlamaServerBackend Backend => Variant.Backend;
    public string BackendDisplay { get; }
    public string SizeDisplay { get; }
    public string AssetName => Variant.AssetName;
    public bool HasCompanion => Variant.CompanionDownloadUrl is not null;
    public bool IsAlreadyInstalled { get; }

    /// <summary>
    /// Upstream's version (e.g. <c>v1.0.0</c>) when this build is the one its
    /// newest versioned release names, otherwise null (ADR-074). Shown as a
    /// badge as it is: a version tag is not translated.
    /// </summary>
    public string? ReleaseVersion { get; }

    public bool IsRelease => ReleaseVersion is not null;

    public LlamaServerVariantRowVm(
        LlamaServerVariant variant, bool alreadyInstalled, string? releaseVersion = null)
    {
        ReleaseVersion = releaseVersion;
        Variant = variant;
        BackendDisplay = LlamaServerBackendFormatter.Display(variant.Backend);
        SizeDisplay = LlamaServerBackendFormatter.FormatBytes(
            variant.Bytes + (variant.CompanionBytes ?? 0));
        IsAlreadyInstalled = alreadyInstalled;
    }
}
