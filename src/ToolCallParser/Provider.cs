namespace ToolCallParser;

/// <summary>
/// Supported LLM providers for tool call parsing.
/// </summary>
public enum Provider
{
    /// <summary>
    /// Auto-detect the provider from the response format.
    /// </summary>
    Auto,

    // ========================================
    // Commercial Cloud Providers
    // ========================================

    /// <summary>
    /// OpenAI API format (function_call, tool_calls).
    /// https://platform.openai.com/docs/guides/function-calling
    /// </summary>
    OpenAI,

    /// <summary>
    /// Azure OpenAI Service format (same as OpenAI).
    /// https://learn.microsoft.com/azure/ai-services/openai/
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// Anthropic Claude API format (tool_use content blocks).
    /// https://docs.anthropic.com/en/docs/build-with-claude/tool-use
    /// </summary>
    Anthropic,

    /// <summary>
    /// Google Gemini/Vertex AI format (functionCall).
    /// https://ai.google.dev/gemini-api/docs/function-calling
    /// </summary>
    Google,

    /// <summary>
    /// xAI Grok API format (OpenAI-compatible with extensions).
    /// https://docs.x.ai/docs/guides/function-calling
    /// </summary>
    XAI,

    /// <summary>
    /// Mistral AI format (OpenAI-compatible tool_calls).
    /// https://docs.mistral.ai/capabilities/function_calling
    /// </summary>
    Mistral,

    /// <summary>
    /// Cohere Command R format (tool_calls with unique structure).
    /// https://docs.cohere.com/docs/tool-use-overview
    /// </summary>
    Cohere,

    /// <summary>
    /// DeepSeek API format (OpenAI-compatible with thinking mode).
    /// https://api-docs.deepseek.com/guides/function_calling
    /// </summary>
    DeepSeek,

    /// <summary>
    /// AWS Bedrock Converse API format (toolUse content blocks).
    /// https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html
    /// </summary>
    Bedrock,

    // ========================================
    // Generic Compatibility Modes
    // ========================================

    /// <summary>
    /// Generic OpenAI-compatible API. Use this for any endpoint that speaks the OpenAI
    /// tool-calling wire format but is not one of the named providers above — including
    /// self-hosted runtimes such as Ollama, vLLM, LM Studio, LocalAI, TGI, GpuStack, and Qwen.
    /// </summary>
    OpenAICompatible,

    /// <summary>
    /// Generic Anthropic-compatible API.
    /// </summary>
    AnthropicCompatible
}

/// <summary>
/// Extension methods for Provider enum.
/// </summary>
public static class ProviderExtensions
{
    /// <summary>
    /// Gets the base format type for a provider.
    /// </summary>
    public static ProviderFormat GetFormat(this Provider provider) => provider switch
    {
        Provider.OpenAI or
        Provider.AzureOpenAI or
        Provider.XAI or
        Provider.Mistral or
        Provider.DeepSeek or
        Provider.OpenAICompatible => ProviderFormat.OpenAI,

        Provider.Anthropic or
        Provider.AnthropicCompatible => ProviderFormat.Anthropic,

        Provider.Google => ProviderFormat.Google,
        Provider.Cohere => ProviderFormat.Cohere,
        Provider.Bedrock => ProviderFormat.Bedrock,

        _ => ProviderFormat.Unknown
    };

    /// <summary>
    /// Checks if the provider uses OpenAI-compatible format.
    /// </summary>
    public static bool IsOpenAICompatible(this Provider provider) =>
        provider.GetFormat() == ProviderFormat.OpenAI;

    /// <summary>
    /// Checks if the provider uses Anthropic-compatible format.
    /// </summary>
    public static bool IsAnthropicCompatible(this Provider provider) =>
        provider.GetFormat() == ProviderFormat.Anthropic;

    /// <summary>
    /// Gets the documentation URL for a provider.
    /// </summary>
    public static string? GetDocumentationUrl(this Provider provider) => provider switch
    {
        Provider.OpenAI => "https://platform.openai.com/docs/guides/function-calling",
        Provider.AzureOpenAI => "https://learn.microsoft.com/azure/ai-services/openai/how-to/function-calling",
        Provider.Anthropic => "https://docs.anthropic.com/en/docs/build-with-claude/tool-use",
        Provider.Google => "https://ai.google.dev/gemini-api/docs/function-calling",
        Provider.XAI => "https://docs.x.ai/docs/guides/function-calling",
        Provider.Mistral => "https://docs.mistral.ai/capabilities/function_calling",
        Provider.Cohere => "https://docs.cohere.com/docs/tool-use-overview",
        Provider.DeepSeek => "https://api-docs.deepseek.com/guides/function_calling",
        Provider.Bedrock => "https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html",
        _ => null
    };
}

/// <summary>
/// Base format types for tool calling.
/// </summary>
public enum ProviderFormat
{
    /// <summary>
    /// Unknown format.
    /// </summary>
    Unknown,

    /// <summary>
    /// OpenAI tool_calls format (used by most providers).
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic tool_use content blocks format.
    /// </summary>
    Anthropic,

    /// <summary>
    /// Google Gemini functionCall format.
    /// </summary>
    Google,

    /// <summary>
    /// Cohere tool_calls format (unique structure).
    /// </summary>
    Cohere,

    /// <summary>
    /// AWS Bedrock toolUse format.
    /// </summary>
    Bedrock
}
