using System.Text.Json;

namespace ToolCallParser.Parsers;

/// <summary>
/// Parser for Google Gemini-style function calls.
/// Handles functionCall format in Gemini's API responses.
///
/// Documentation: https://ai.google.dev/gemini-api/docs/function-calling
/// </summary>
public sealed class GoogleToolCallParser : IToolCallParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public Provider Provider => Provider.Google;

    /// <inheritdoc />
    public IReadOnlyList<ToolCall> Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(response);
        return Parse(doc.RootElement);
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCall> Parse(JsonElement element)
    {
        var results = new List<ToolCall>();

        // Try to find functionCall in various locations
        if (TryGetFunctionCalls(element, out var functionCalls))
        {
            foreach (var functionCall in functionCalls)
            {
                var parsed = ParseFunctionCall(functionCall);
                if (parsed != null)
                {
                    results.Add(parsed);
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public bool HasToolCalls(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(response);
        return HasToolCalls(doc.RootElement);
    }

    /// <inheritdoc />
    public bool HasToolCalls(JsonElement element)
    {
        return TryGetFunctionCalls(element, out var calls) && calls.Count > 0;
    }

    /// <inheritdoc />
    public string FormatResults(IEnumerable<ToolCallResult> results)
    {
        // Gemini expects functionResponse format
        var functionResponses = results.Select(r => new
        {
            functionResponse = new
            {
                name = r.ToolName ?? "",
                response = new
                {
                    result = r.Content
                }
            }
        });

        return JsonSerializer.Serialize(functionResponses, JsonOptions);
    }

    private static bool TryGetFunctionCalls(JsonElement element, out List<JsonElement> functionCalls)
    {
        functionCalls = [];

        // Check candidates[].content.parts[].functionCall (API response format)
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
                        if (part.TryGetProperty("functionCall", out var functionCall))
                        {
                            functionCalls.Add(functionCall);
                        }
                    }
                }
            }
        }

        // Check content.parts[].functionCall (message format)
        if (element.TryGetProperty("content", out var directContent) &&
            directContent.TryGetProperty("parts", out var directParts) &&
            directParts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in directParts.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var functionCall))
                {
                    functionCalls.Add(functionCall);
                }
            }
        }

        // Check parts[].functionCall directly
        if (element.TryGetProperty("parts", out var partsOnly) && partsOnly.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in partsOnly.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var functionCall))
                {
                    functionCalls.Add(functionCall);
                }
            }
        }

        // Check direct functionCall
        if (element.TryGetProperty("functionCall", out var directFunctionCall))
        {
            functionCalls.Add(directFunctionCall);
        }

        return functionCalls.Count > 0;
    }

    private static ToolCall? ParseFunctionCall(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var arguments = "{}";
        if (element.TryGetProperty("args", out var argsElement))
        {
            arguments = argsElement.GetRawText();
        }

        // Generate an ID since Gemini doesn't provide one
        var id = $"call_{Guid.NewGuid():N}"[..29];

        return new ToolCall
        {
            Id = id,
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }
}
