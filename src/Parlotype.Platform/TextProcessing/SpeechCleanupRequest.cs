using System.Text.Json;
using System.Text.Json.Nodes;
using Parlotype.Core.TextProcessing;

namespace Parlotype.Platform.TextProcessing;

/// <summary>
/// The <c>/v1/chat/completions</c> exchange for a speech cleanup pass: builds the
/// request body and reads the reply. Pure, so the wire format is testable
/// without a server.
/// </summary>
internal static class SpeechCleanupRequest
{
    /// <summary>
    /// Builds the request body for S1-mini.
    /// </summary>
    /// <remarks>
    /// <para><c>temperature: 0</c> is sent on every request. The model is trained
    /// for greedy decoding, while the GGUF's own metadata carries Qwen3's 0.6 and
    /// llama.cpp defaults to 0.8, so relying on either default samples.</para>
    /// <para><c>enable_thinking: false</c> repeats what the server is started
    /// with. The template is Qwen3's and thinks by default; S1-mini was trained
    /// with thinking off and returns nothing usable otherwise. Sending it per
    /// request as well keeps the output right if someone points the feature at a
    /// server launched without the flag.</para>
    /// </remarks>
    public static JsonObject Build(SpeechCleanupOptions options, string transcript) => new()
    {
        ["stream"] = false,
        ["temperature"] = 0,
        ["max_tokens"] = MaxOutputTokens(transcript),
        ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
        ["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = S1MiniPrompt.SystemPrompt },
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = S1MiniPrompt.BuildUserMessage(options, transcript),
            },
        },
    };

    /// <summary>
    /// Upper bound on generated tokens. Cleaned text is about as long as the
    /// transcript, and English runs near four characters per token, so half the
    /// character count is a generous ceiling. Its job is to stop a model that
    /// loops, not to shape normal output.
    /// </summary>
    internal static int MaxOutputTokens(string transcript) => transcript.Length / 2 + 64;

    /// <summary>Reads <c>choices[0].message.content</c>, trimmed. Empty is valid.</summary>
    public static string ParseContent(JsonElement root)
    {
        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");

        return content.ValueKind == JsonValueKind.String
            ? content.GetString()!.Trim()
            : string.Empty;
    }
}
