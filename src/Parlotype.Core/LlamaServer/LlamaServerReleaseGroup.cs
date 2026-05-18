namespace Parlotype.Core.LlamaServer;

/// <summary>One GitHub release (e.g. <c>b9198</c>) with its variants.</summary>
public sealed record LlamaServerReleaseGroup(
    string Build,
    IReadOnlyList<LlamaServerVariant> Variants);
