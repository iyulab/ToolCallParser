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

    /// <summary>
    /// OpenAI API format (function_call, tool_calls).
    /// </summary>
    OpenAI,

    /// <summary>
    /// Azure OpenAI Service format (similar to OpenAI).
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// Anthropic Claude API format (tool_use content blocks).
    /// </summary>
    Anthropic,

    /// <summary>
    /// Google Gemini/Vertex AI format.
    /// </summary>
    Google,

    /// <summary>
    /// Mistral AI format.
    /// </summary>
    Mistral,

    /// <summary>
    /// Cohere Command R format.
    /// </summary>
    Cohere,

    /// <summary>
    /// AWS Bedrock format (wraps other providers).
    /// </summary>
    Bedrock,

    /// <summary>
    /// Ollama local models (OpenAI-compatible).
    /// </summary>
    Ollama,

    /// <summary>
    /// GPUStack local inference (OpenAI-compatible).
    /// </summary>
    GpuStack,

    /// <summary>
    /// Generic OpenAI-compatible API.
    /// </summary>
    OpenAICompatible
}
