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
    public bool CanParse(JsonElement element)
    {
        // choices array (OpenAI response format)
        if (element.TryGetProperty("choices", out _))
        {
            return true;
        }

        // tool_calls directly
        if (element.TryGetProperty("tool_calls", out _))
        {
            return true;
        }

        // function_call (legacy format)
        if (element.TryGetProperty("function_call", out _))
        {
            return true;
        }

        // message object with tool_calls / function_call
        if (element.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetProperty("tool_calls", out _) || message.TryGetProperty("function_call", out _))
            {
                return true;
            }
        }

        // Responses API: output[] items (or a bare item) with type == "function_call"
        if (HasResponsesFunctionCallItems(element))
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

        // Responses API: output[] items with type == "function_call"
        ParseResponsesFunctionCallItems(element, results);

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
            || TryGetFunctionCallElement(element, out _)
            || HasResponsesFunctionCallItems(element);
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

    /// <summary>
    /// Detects Responses API function-call items: either a top-level <c>output</c> array
    /// containing <c>{"type":"function_call", ...}</c> items, or a bare such item. Models
    /// exclusively served through the Responses API (e.g. gpt-5.4-pro) emit this shape
    /// instead of Chat Completions <c>tool_calls</c>.
    /// </summary>
    private static bool HasResponsesFunctionCallItems(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (IsResponsesFunctionCallItem(element))
        {
            return true;
        }

        if (element.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (IsResponsesFunctionCallItem(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsResponsesFunctionCallItem(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "function_call"
            && element.TryGetProperty("name", out _);

    private static void ParseResponsesFunctionCallItems(JsonElement element, List<ToolCall> results)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (IsResponsesFunctionCallItem(element))
        {
            var parsed = ParseResponsesFunctionCallItem(element);
            if (parsed != null)
            {
                results.Add(parsed);
            }
            return;
        }

        if (element.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!IsResponsesFunctionCallItem(item))
                {
                    continue;
                }

                var parsed = ParseResponsesFunctionCallItem(item);
                if (parsed != null)
                {
                    results.Add(parsed);
                }
            }
        }
    }

    private static ToolCall? ParseResponsesFunctionCallItem(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        // call_id is the reference id used to submit results; fall back to the item id.
        var id = element.TryGetProperty("call_id", out var callId) && callId.ValueKind == JsonValueKind.String
            ? callId.GetString()
            : element.TryGetProperty("id", out var itemId) && itemId.ValueKind == JsonValueKind.String
                ? itemId.GetString()
                : null;

        var arguments = "{}";
        if (element.TryGetProperty("arguments", out var argsElement))
        {
            // Arguments are a JSON-encoded string in the Responses API; tolerate an
            // already-parsed object as well (seen in adjacent item-style formats).
            arguments = argsElement.ValueKind == JsonValueKind.String
                ? argsElement.GetString() ?? "{}"
                : argsElement.GetRawText();
        }

        return new ToolCall
        {
            Id = id ?? $"call_{Guid.NewGuid():N}"[..29],
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
