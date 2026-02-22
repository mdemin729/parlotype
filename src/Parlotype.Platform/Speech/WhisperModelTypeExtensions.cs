using Parlotype.Core.Speech;
using Whisper.net.Ggml;

namespace Parlotype.Platform.Speech;

/// <summary>Maps the Core-level <see cref="WhisperModelType"/> to the Whisper.net <see cref="GgmlType"/>.</summary>
internal static class WhisperModelTypeExtensions
{
    public static GgmlType ToGgmlType(this WhisperModelType type) => type switch
    {
        WhisperModelType.Tiny         => GgmlType.Tiny,
        WhisperModelType.TinyEn       => GgmlType.TinyEn,
        WhisperModelType.Base         => GgmlType.Base,
        WhisperModelType.BaseEn       => GgmlType.BaseEn,
        WhisperModelType.Small        => GgmlType.Small,
        WhisperModelType.SmallEn      => GgmlType.SmallEn,
        WhisperModelType.Medium       => GgmlType.Medium,
        WhisperModelType.MediumEn     => GgmlType.MediumEn,
        WhisperModelType.LargeV1      => GgmlType.LargeV1,
        WhisperModelType.LargeV2      => GgmlType.LargeV2,
        WhisperModelType.LargeV3      => GgmlType.LargeV3,
        WhisperModelType.LargeV3Turbo => GgmlType.LargeV3Turbo,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Whisper model type"),
    };
}
