namespace Parlotype.Core.Speech;

/// <summary>
/// Describes a selectable transcription / translation language.
/// </summary>
/// <param name="Code">
/// Language identifier. Either an ISO 639-1 code (e.g. <c>"en"</c>, <c>"ru"</c>),
/// a Whisper-specific code (e.g. <c>"yue"</c>, <c>"jw"</c>), or one of the special
/// sentinels <see cref="LanguageCatalog.AutoDetectCode"/> /
/// <see cref="LanguageCatalog.NoTranslationCode"/>.
/// </param>
/// <param name="EnglishName">English display name (e.g. "Russian").</param>
/// <param name="NativeName">Endonym / native display name (e.g. "Русский").</param>
public sealed record LanguageInfo(string Code, string EnglishName, string NativeName);
