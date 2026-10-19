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
        if (element.TryGetObjectProperty("choices", out _))
        {
            return true;
        }

        // tool_calls directly
        if (element.TryGetObjectProperty("tool_calls", out _))
        {
            return true;
        }

        // function_call (legacy format)
        if (element.TryGetObjectProperty("function_call", out _))
        {
            return true;
        }

        // message object with tool_calls / function_call
        if (element.TryGetObjectProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetObjectProperty("tool_calls", out _) || message.TryGetObjectProperty("function_call", out _))
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
        if (!JsonText.TryParse(response, out var doc))
        {
            return [];
        }

        using (doc)
        {
            return Parse(doc.RootElement);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCall> Parse(JsonElement element)
    {
        var results = new List<ToolCall>();

        // Try to find tool_calls in various locations
        if (TryGetToolCallsElement(element, out var toolCallsElement, out var isDelta))
        {
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                if (isDelta && !IsCompleteDeltaCall(toolCall))
                {
                    continue;
                }

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
        if (!JsonText.TryParse(response, out var doc))
        {
            return false;
        }

        using (doc)
        {
            return HasToolCalls(doc.RootElement);
        }
    }

    /// <inheritdoc />
    public bool HasToolCalls(JsonElement element)
    {
        return TryGetToolCallsElement(element, out var toolCalls, out var isDelta)
                && toolCalls.EnumerateArray().Any(c => !isDelta || IsCompleteDeltaCall(c))
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

    private static bool TryGetToolCallsElement(JsonElement element, out JsonElement toolCalls, out bool isDelta)
    {
        toolCalls = default;
        isDelta = false;

        // Direct tool_calls array
        if (element.TryGetObjectProperty("tool_calls", out toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        // Nested in choices[].message.tool_calls (API response format)
        if (element.TryGetObjectProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetObjectProperty("message", out var message) &&
                    message.TryGetObjectProperty("tool_calls", out toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }

                // A streaming chunk. Only calls complete within the chunk are read (see IsCompleteDeltaCall).
                if (choice.TryGetObjectProperty("delta", out var delta) &&
                    delta.TryGetObjectProperty("tool_calls", out toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array)
                {
                    isDelta = true;
                    return true;
                }
            }
        }

        // Nested in message.tool_calls
        if (element.TryGetObjectProperty("message", out var msg) &&
            msg.ValueKind == JsonValueKind.Object &&
            msg.TryGetObjectProperty("tool_calls", out toolCalls) &&
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
        if (element.TryGetObjectProperty("function_call", out functionCall) && functionCall.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        // Nested in choices[].message.function_call
        if (element.TryGetObjectProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetObjectProperty("message", out var message) &&
                    message.TryGetObjectProperty("function_call", out functionCall) &&
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
        if (!element.TryGetObjectProperty("function", out var functionElement))
        {
            return null;
        }

        if (!functionElement.TryGetObjectProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var name = nameElement.GetString()!;

        // Ollama's native /api/chat gives no id; a call without one is still a call.
        var id = element.TryGetObjectProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : NewCallId();

        return new ToolCall
        {
            Id = id,
            Name = name,
            Arguments = ReadArguments(functionElement, name)
        };
    }

    /// <summary>
    /// Whether a streamed <c>tool_calls</c> entry is a whole call: a name and arguments that already read as a JSON
    /// object. A chunk usually carries a fragment — the id and name with <c>arguments: ""</c>, or an arguments fragment
    /// with no name — which is not a call until the stream is accumulated. Some servers send a whole call in one chunk.
    /// </summary>
    private static bool IsCompleteDeltaCall(JsonElement toolCall)
    {
        if (!toolCall.TryGetObjectProperty("function", out var function)
            || !function.TryGetObjectProperty("name", out var name) || name.ValueKind != JsonValueKind.String
            || !function.TryGetObjectProperty("arguments", out var args))
        {
            return false;
        }

        if (args.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        if (args.ValueKind != JsonValueKind.String || !JsonText.TryParse(args.GetString(), out var doc))
        {
            return false;
        }

        using (doc)
        {
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// The call's arguments as a JSON object text. The wire gives them as a JSON-encoded string (OpenAI) or as an
    /// object (Ollama's native API); either is accepted, absent or <c>null</c> is <c>{}</c>.
    /// </summary>
    /// <exception cref="FormatException">The arguments are not a JSON object — the model wrote a call whose arguments
    /// cannot be read.</exception>
    private static string ReadArguments(JsonElement owner, string name)
    {
        if (!owner.TryGetObjectProperty("arguments", out var args) || args.ValueKind == JsonValueKind.Null)
        {
            return "{}";
        }

        if (args.ValueKind == JsonValueKind.Object)
        {
            return args.GetRawText();
        }

        if (args.ValueKind == JsonValueKind.String)
        {
            var text = args.GetString()!;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "{}";
            }

            if (JsonText.TryParse(text, out var doc))
            {
                using (doc)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        return text;
                    }
                }
            }
        }

        throw new FormatException($"Tool call '{name}' has arguments that are not a JSON object.");
    }

    private static string NewCallId() => $"call_{Guid.NewGuid():N}"[..29];

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

        if (element.TryGetObjectProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
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
            && element.TryGetObjectProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "function_call"
            && element.TryGetObjectProperty("name", out _);

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

        if (element.TryGetObjectProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
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
        if (!element.TryGetObjectProperty("name", out var nameElement))
        {
            return null;
        }

        // call_id is the reference id used to submit results; fall back to the item id.
        var id = element.TryGetObjectProperty("call_id", out var callId) && callId.ValueKind == JsonValueKind.String
            ? callId.GetString()
            : element.TryGetObjectProperty("id", out var itemId) && itemId.ValueKind == JsonValueKind.String
                ? itemId.GetString()
                : null;

        var name = nameElement.GetString() ?? string.Empty;

        return new ToolCall
        {
            Id = id ?? NewCallId(),
            Name = name,
            // A JSON-encoded string in the Responses API; an already-parsed object is accepted too.
            Arguments = ReadArguments(element, name)
        };
    }

    private static ToolCall? ParseFunctionCall(JsonElement element)
    {
        if (!element.TryGetObjectProperty("name", out var nameElement))
        {
            return null;
        }

        var name = nameElement.GetString() ?? string.Empty;

        // Legacy function calls carry no id.
        return new ToolCall
        {
            Id = NewCallId(),
            Name = name,
            Arguments = ReadArguments(element, name)
        };
    }
}
