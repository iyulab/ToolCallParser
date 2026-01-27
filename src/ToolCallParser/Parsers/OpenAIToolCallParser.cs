using System.Text.Json;

namespace ToolCallParser.Parsers;

/// <summary>
/// Parser for OpenAI-style tool calls.
/// Handles both the new tool_calls format and legacy function_call format.
/// </summary>
public sealed class OpenAIToolCallParser : IToolCallParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public Provider Provider => Provider.OpenAI;

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

        // Try to find tool_calls in various locations
        if (TryGetToolCallsElement(element, out var toolCallsElement))
        {
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                var parsed = ParseToolCall(toolCall);
                if (parsed != null)
                {
                    results.Add(parsed);
                }
            }
        }

        // Also check for legacy function_call format
        if (TryGetFunctionCallElement(element, out var functionCallElement))
        {
            var parsed = ParseFunctionCall(functionCallElement);
            if (parsed != null)
            {
                results.Add(parsed);
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
        return TryGetToolCallsElement(element, out var toolCalls) && toolCalls.GetArrayLength() > 0
            || TryGetFunctionCallElement(element, out _);
    }

    /// <inheritdoc />
    public string FormatResults(IEnumerable<ToolCallResult> results)
    {
        var messages = results.Select(r => new
        {
            role = "tool",
            tool_call_id = r.ToolCallId,
            content = r.Content
        });

        return JsonSerializer.Serialize(messages, JsonOptions);
    }

    private static bool TryGetToolCallsElement(JsonElement element, out JsonElement toolCalls)
    {
        toolCalls = default;

        // Direct tool_calls array
        if (element.TryGetProperty("tool_calls", out toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        // Nested in choices[].message.tool_calls (API response format)
        if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("tool_calls", out toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }

                // Also check delta for streaming responses
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("tool_calls", out toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }
            }
        }

        // Nested in message.tool_calls
        if (element.TryGetProperty("message", out var msg) &&
            msg.ValueKind == JsonValueKind.Object &&
            msg.TryGetProperty("tool_calls", out toolCalls) &&
            toolCalls.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        return false;
    }

    private static bool TryGetFunctionCallElement(JsonElement element, out JsonElement functionCall)
    {
        functionCall = default;

        // Direct function_call
        if (element.TryGetProperty("function_call", out functionCall) && functionCall.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        // Nested in choices[].message.function_call
        if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("function_call", out functionCall) &&
                    functionCall.ValueKind == JsonValueKind.Object)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ToolCall? ParseToolCall(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        if (!element.TryGetProperty("function", out var functionElement))
        {
            return null;
        }

        if (!functionElement.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var arguments = "{}";
        if (functionElement.TryGetProperty("arguments", out var argsElement))
        {
            arguments = argsElement.GetString() ?? "{}";
        }

        return new ToolCall
        {
            Id = idElement.GetString() ?? string.Empty,
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }

    private static ToolCall? ParseFunctionCall(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var arguments = "{}";
        if (element.TryGetProperty("arguments", out var argsElement))
        {
            arguments = argsElement.GetString() ?? "{}";
        }

        // Generate an ID for legacy function calls
        var id = $"call_{Guid.NewGuid():N}"[..29];

        return new ToolCall
        {
            Id = id,
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }
}
