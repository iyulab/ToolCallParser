using System.Collections.Frozen;
using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser;

/// <summary>
/// Factory for creating and using tool call parsers.
///
/// Supported formats:
/// - OpenAI-compatible: OpenAI, Azure, Mistral, xAI, DeepSeek, and any OpenAI-compatible
///   endpoint via <see cref="Provider.OpenAICompatible"/> (self-hosted runtimes such as
///   Ollama/vLLM/LM Studio use OpenAICompatible — they share the OpenAI wire format).
/// - Anthropic: Claude API (tool_use content blocks)
/// - Google: Gemini API (functionCall)
/// - Cohere: Command R (tool_calls with unique structure)
/// - Bedrock: AWS Converse API (toolUse content blocks)
/// </summary>
public static class ToolCallParserFactory
{
    // Read-only after construction: one parser instance per supported provider. Frozen for
    // fast, allocation-free lookup. Custom-parser registration was removed (unused in
    // production; process-global mutation of a static table is a footgun) — implement
    // IToolCallParser and call its methods directly instead.
    private static readonly FrozenDictionary<Provider, IToolCallParser> Parsers =
        new Dictionary<Provider, IToolCallParser>
        {
            // OpenAI-compatible providers (all share OpenAIToolCallParser)
            { Provider.OpenAI, new OpenAIToolCallParser() },
            { Provider.AzureOpenAI, new OpenAIToolCallParser() },
            { Provider.XAI, new OpenAIToolCallParser() },
            { Provider.Mistral, new OpenAIToolCallParser() },
            { Provider.DeepSeek, new OpenAIToolCallParser() },
            { Provider.OpenAICompatible, new OpenAIToolCallParser() },

            // Anthropic-compatible providers
            { Provider.Anthropic, new AnthropicToolCallParser() },
            { Provider.AnthropicCompatible, new AnthropicToolCallParser() },

            // Unique format providers
            { Provider.Google, new GoogleToolCallParser() },
            { Provider.Cohere, new CohereToolCallParser() },
            { Provider.Bedrock, new BedrockToolCallParser() }
        }.ToFrozenDictionary();

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
    /// Provider probe order for detection. Specific/unique formats are probed before the
    /// broad OpenAI-compatible format (whose <c>choices</c>/<c>tool_calls</c> markers are the
    /// most permissive), so a response that matches a unique format is never misclassified as OpenAI.
    /// </summary>
    private static readonly Provider[] DetectionOrder =
    [
        Provider.Anthropic,
        Provider.Google,
        Provider.Bedrock,
        Provider.Cohere,
        Provider.OpenAI
    ];

    /// <summary>
    /// Detects the provider from a JsonElement.
    /// </summary>
    /// <param name="element">The JSON element</param>
    /// <returns>The detected provider or Auto if unknown</returns>
    public static Provider DetectProvider(JsonElement element)
    {
        // Single source of truth: each parser owns its own format recognition via CanParse.
        // The factory only decides probe order, not what each format looks like.
        foreach (var provider in DetectionOrder)
        {
            if (Parsers.TryGetValue(provider, out var parser) && parser.CanParse(element))
            {
                return provider;
            }
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
    /// Gets all registered providers.
    /// </summary>
    public static IEnumerable<Provider> GetRegisteredProviders() => Parsers.Keys;

    /// <summary>
    /// Gets all OpenAI-compatible providers.
    /// </summary>
    public static IEnumerable<Provider> GetOpenAICompatibleProviders() =>
        Parsers.Keys.Where(p => p.IsOpenAICompatible());
}
