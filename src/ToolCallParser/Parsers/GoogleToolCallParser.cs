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
    public bool CanParse(JsonElement element)
    {
        // candidates array (Gemini response format)
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

        // functionCall directly
        if (element.TryGetProperty("functionCall", out _))
        {
            return true;
        }

        // parts with functionCall
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

        // Interactions API: steps[] items with type == "function_call"
        if (TryGetInteractionSteps(element, out var steps) && steps.Count > 0)
        {
            return true;
        }

        return false;
    }

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

        // Interactions API steps carry the call in a different shape than generateContent parts
        if (TryGetInteractionSteps(element, out var steps))
        {
            foreach (var step in steps)
            {
                var parsed = ParseInteractionStep(step);
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
        return (TryGetFunctionCalls(element, out var calls) && calls.Count > 0)
            || (TryGetInteractionSteps(element, out var steps) && steps.Count > 0);
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

    /// <summary>
    /// Collects Interactions API function-call steps. An Interaction records its history as a
    /// chronological <c>steps</c> array mixing thoughts, tool calls, tool results and the final
    /// model output, so only the <c>function_call</c> entries are taken.
    /// </summary>
    /// <remarks>
    /// Only the <c>steps</c> envelope is recognised, deliberately. A bare step is indistinguishable
    /// from an OpenAI Responses <c>function_call</c> item (both are an object with
    /// <c>type</c>/<c>name</c>), so claiming it here would make detection ambiguous rather than
    /// more complete.
    /// </remarks>
    private static bool TryGetInteractionSteps(JsonElement element, out List<JsonElement> steps)
    {
        steps = [];

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("steps", out var stepsElement) ||
            stepsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var step in stepsElement.EnumerateArray())
        {
            if (IsFunctionCallStep(step))
            {
                steps.Add(step);
            }
        }

        return steps.Count > 0;
    }

    private static bool IsFunctionCallStep(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "function_call"
            && element.TryGetProperty("name", out _);

    private static ToolCall? ParseInteractionStep(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var arguments = "{}";
        if (element.TryGetProperty("arguments", out var argsElement))
        {
            arguments = argsElement.GetRawText();
        }

        // Unlike generateContent, the Interactions API supplies the id that the matching
        // function_result must reference, so it is preserved rather than generated.
        var id = element.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : null;

        return new ToolCall
        {
            Id = id ?? $"call_{Guid.NewGuid():N}"[..29],
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }
}
