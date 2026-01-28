using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser;

/// <summary>
/// Factory for creating and using tool call parsers.
///
/// Supported formats:
/// - OpenAI-compatible: OpenAI, Azure, Mistral, xAI, DeepSeek, Ollama, GpuStack, vLLM, Qwen, etc.
/// - Anthropic: Claude API (tool_use content blocks)
/// - Google: Gemini API (functionCall)
/// - Cohere: Command R (tool_calls with unique structure)
/// - Bedrock: AWS Converse API (toolUse content blocks)
/// </summary>
public static class ToolCallParserFactory
{
    private static readonly Dictionary<Provider, IToolCallParser> Parsers = new()
    {
        // OpenAI-compatible providers
        { Provider.OpenAI, new OpenAIToolCallParser() },
        { Provider.AzureOpenAI, new OpenAIToolCallParser() },
        { Provider.XAI, new OpenAIToolCallParser() },
        { Provider.Mistral, new OpenAIToolCallParser() },
        { Provider.DeepSeek, new OpenAIToolCallParser() },
        { Provider.Ollama, new OpenAIToolCallParser() },
        { Provider.GpuStack, new OpenAIToolCallParser() },
        { Provider.VLLM, new OpenAIToolCallParser() },
        { Provider.Qwen, new OpenAIToolCallParser() },
        { Provider.LMStudio, new OpenAIToolCallParser() },
        { Provider.LocalAI, new OpenAIToolCallParser() },
        { Provider.TGI, new OpenAIToolCallParser() },
        { Provider.OpenAICompatible, new OpenAIToolCallParser() },

        // Anthropic-compatible providers
        { Provider.Anthropic, new AnthropicToolCallParser() },
        { Provider.AnthropicCompatible, new AnthropicToolCallParser() },

        // Unique format providers
        { Provider.Google, new GoogleToolCallParser() },
        { Provider.Cohere, new CohereToolCallParser() },
        { Provider.Bedrock, new BedrockToolCallParser() }
    };

    /// <summary>
    /// Gets a parser for the specified provider.
    /// </summary>
    /// <param name="provider">The provider type</param>
    /// <returns>The appropriate parser</returns>
    /// <exception cref="NotSupportedException">If the provider is not supported</exception>
    public static IToolCallParser GetParser(Provider provider)
    {
        if (provider == Provider.Auto)
        {
            throw new ArgumentException("Use Parse() or DetectProvider() for auto-detection", nameof(provider));
        }

        if (Parsers.TryGetValue(provider, out var parser))
        {
            return parser;
        }

        throw new NotSupportedException($"Provider {provider} is not yet supported");
    }

    /// <summary>
    /// Tries to get a parser for the specified provider.
    /// </summary>
    /// <param name="provider">The provider type</param>
    /// <param name="parser">The parser if found</param>
    /// <returns>True if parser was found</returns>
    public static bool TryGetParser(Provider provider, out IToolCallParser? parser)
    {
        if (provider == Provider.Auto)
        {
            parser = null;
            return false;
        }

        return Parsers.TryGetValue(provider, out parser);
    }

    /// <summary>
    /// Parses tool calls from a response, auto-detecting the provider.
    /// </summary>
    /// <param name="response">The raw JSON response</param>
    /// <returns>List of parsed tool calls</returns>
    public static IReadOnlyList<ToolCall> Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(response);
        return Parse(doc.RootElement);
    }

    /// <summary>
    /// Parses tool calls from a JsonElement, auto-detecting the provider.
    /// </summary>
    /// <param name="element">The JSON element</param>
    /// <returns>List of parsed tool calls</returns>
    public static IReadOnlyList<ToolCall> Parse(JsonElement element)
    {
        var provider = DetectProvider(element);
        if (provider != Provider.Auto && Parsers.TryGetValue(provider, out var parser))
        {
            return parser.Parse(element);
        }

        // Could not detect, try all unique parsers in order of likelihood
        var parseOrder = new[]
        {
            Provider.OpenAI,      // Most common
            Provider.Anthropic,   // Second most common
            Provider.Google,      // Unique format
            Provider.Bedrock,     // Unique format
            Provider.Cohere       // Unique format
        };

        foreach (var p in parseOrder)
        {
            if (Parsers.TryGetValue(p, out var fallbackParser))
            {
                var results = fallbackParser.Parse(element);
                if (results.Count > 0)
                {
                    return results;
                }
            }
        }

        return [];
    }

    /// <summary>
    /// Detects the provider from a response format.
    /// </summary>
    /// <param name="response">The raw JSON response</param>
    /// <returns>The detected provider or Auto if unknown</returns>
    public static Provider DetectProvider(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return Provider.Auto;
        }

        using var doc = JsonDocument.Parse(response);
        return DetectProvider(doc.RootElement);
    }

    /// <summary>
    /// Detects the provider from a JsonElement.
    /// </summary>
    /// <param name="element">The JSON element</param>
    /// <returns>The detected provider or Auto if unknown</returns>
    public static Provider DetectProvider(JsonElement element)
    {
        // Check for Anthropic-specific fields
        if (IsAnthropicFormat(element))
        {
            return Provider.Anthropic;
        }

        // Check for Google Gemini format
        if (IsGoogleFormat(element))
        {
            return Provider.Google;
        }

        // Check for AWS Bedrock format
        if (IsBedrockFormat(element))
        {
            return Provider.Bedrock;
        }

        // Check for Cohere format
        if (IsCohereFormat(element))
        {
            return Provider.Cohere;
        }

        // Check for OpenAI-compatible format (most common)
        if (IsOpenAIFormat(element))
        {
            return Provider.OpenAI;
        }

        return Provider.Auto;
    }

    /// <summary>
    /// Checks if any tool calls are present in the response.
    /// </summary>
    /// <param name="response">The raw JSON response</param>
    /// <returns>True if tool calls are present</returns>
    public static bool HasToolCalls(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(response);
        return HasToolCalls(doc.RootElement);
    }

    /// <summary>
    /// Checks if any tool calls are present in the JsonElement.
    /// </summary>
    /// <param name="element">The JSON element</param>
    /// <returns>True if tool calls are present</returns>
    public static bool HasToolCalls(JsonElement element)
    {
        var provider = DetectProvider(element);
        if (provider != Provider.Auto && Parsers.TryGetValue(provider, out var parser))
        {
            return parser.HasToolCalls(element);
        }

        // Try all parsers
        foreach (var p in Parsers.Values)
        {
            if (p.HasToolCalls(element))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Registers a custom parser for a provider.
    /// </summary>
    /// <param name="provider">The provider type</param>
    /// <param name="parser">The parser implementation</param>
    public static void RegisterParser(Provider provider, IToolCallParser parser)
    {
        Parsers[provider] = parser;
    }

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public static IEnumerable<Provider> GetRegisteredProviders() => Parsers.Keys;

    /// <summary>
    /// Gets all OpenAI-compatible providers.
    /// </summary>
    public static IEnumerable<Provider> GetOpenAICompatibleProviders() =>
        Parsers.Keys.Where(p => p.IsOpenAICompatible());

    private static bool IsAnthropicFormat(JsonElement element)
    {
        // Check for stop_reason (Anthropic-specific field name)
        if (element.TryGetProperty("stop_reason", out _))
        {
            return true;
        }

        // Check for content array with tool_use blocks
        if (element.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();
                    if (type == "tool_use" || type == "tool_result")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsGoogleFormat(JsonElement element)
    {
        // Check for candidates array (Gemini response format)
        if (element.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("functionCall", out _))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // Check for functionCall directly
        if (element.TryGetProperty("functionCall", out _))
        {
            return true;
        }

        // Check for parts with functionCall
        if (element.TryGetProperty("parts", out var directParts) && directParts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in directParts.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsBedrockFormat(JsonElement element)
    {
        // Check for stopReason (Bedrock uses camelCase)
        if (element.TryGetProperty("stopReason", out var stopReason))
        {
            var reason = stopReason.GetString();
            if (reason == "tool_use")
            {
                return true;
            }
        }

        // Check for toolUse blocks
        if (element.TryGetProperty("output", out var output) &&
            output.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("toolUse", out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCohereFormat(JsonElement element)
    {
        // Check for finish_reason TOOL_CALL
        if (element.TryGetProperty("finish_reason", out var finishReason))
        {
            var reason = finishReason.GetString();
            if (reason == "TOOL_CALL")
            {
                return true;
            }
        }

        // Check for tool_plan (Cohere-specific)
        if (element.TryGetProperty("tool_plan", out _))
        {
            return true;
        }

        // Check for actions array (multi-step)
        if (element.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                if (action.TryGetProperty("tool_name", out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsOpenAIFormat(JsonElement element)
    {
        // Check for choices array (OpenAI response format)
        if (element.TryGetProperty("choices", out _))
        {
            return true;
        }

        // Check for tool_calls directly
        if (element.TryGetProperty("tool_calls", out _))
        {
            return true;
        }

        // Check for function_call (legacy format)
        if (element.TryGetProperty("function_call", out _))
        {
            return true;
        }

        // Check for message with tool_calls (only if message is an object)
        if (element.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetProperty("tool_calls", out _) || message.TryGetProperty("function_call", out _))
            {
                return true;
            }
        }

        return false;
    }
}
