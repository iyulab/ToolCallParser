using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser;

/// <summary>
/// Factory for creating tool call parsers.
/// </summary>
public static class ToolCallParserFactory
{
    private static readonly Dictionary<Provider, IToolCallParser> Parsers = new()
    {
        { Provider.OpenAI, new OpenAIToolCallParser() },
        { Provider.AzureOpenAI, new OpenAIToolCallParser() },
        { Provider.Ollama, new OpenAIToolCallParser() },
        { Provider.GpuStack, new OpenAIToolCallParser() },
        { Provider.OpenAICompatible, new OpenAIToolCallParser() },
        { Provider.Anthropic, new AnthropicToolCallParser() }
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
        if (provider == Provider.Auto)
        {
            // Could not detect, try all parsers
            foreach (var parser in Parsers.Values)
            {
                var results = parser.Parse(element);
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return [];
        }

        return Parsers[provider].Parse(element);
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
        if (element.TryGetProperty("stop_reason", out _) ||
            element.TryGetProperty("content", out var content) && IsAnthropicContentFormat(content))
        {
            return Provider.Anthropic;
        }

        // Check for OpenAI-specific fields
        if (element.TryGetProperty("choices", out _) ||
            element.TryGetProperty("tool_calls", out _) ||
            element.TryGetProperty("function_call", out _))
        {
            return Provider.OpenAI;
        }

        // Check for message.tool_calls (OpenAI format)
        if (element.TryGetProperty("message", out var message) &&
            (message.TryGetProperty("tool_calls", out _) || message.TryGetProperty("function_call", out _)))
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
        foreach (var parser in Parsers.Values)
        {
            if (parser.HasToolCalls(element))
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

    private static bool IsAnthropicContentFormat(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                if (type == "tool_use" || type == "text")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
