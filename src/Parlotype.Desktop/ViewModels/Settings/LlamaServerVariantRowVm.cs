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

    public LlamaServerVariantRowVm(LlamaServerVariant variant, bool alreadyInstalled)
    {
        Variant = variant;
        BackendDisplay = LlamaServerBackendFormatter.Display(variant.Backend);
        SizeDisplay = LlamaServerBackendFormatter.FormatBytes(
            variant.Bytes + (variant.CompanionBytes ?? 0));
        IsAlreadyInstalled = alreadyInstalled;
    }
}
