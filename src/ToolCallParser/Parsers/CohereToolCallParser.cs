using System.Text.Json;

namespace ToolCallParser.Parsers;

/// <summary>
/// Parser for Cohere Command R-style tool calls.
/// Handles Cohere's unique tool_calls format.
///
/// Documentation: https://docs.cohere.com/docs/tool-use-overview
/// </summary>
public sealed class CohereToolCallParser : IToolCallParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc />
    public Provider Provider => Provider.Cohere;

    /// <inheritdoc />
    public bool CanParse(JsonElement element)
    {
        // finish_reason == "TOOL_CALL"
        if (element.TryGetObjectProperty("finish_reason", out var finishReason) &&
            finishReason.GetString() == "TOOL_CALL")
        {
            return true;
        }

        // tool_plan (Cohere-specific)
        if (element.TryGetObjectProperty("tool_plan", out _))
        {
            return true;
        }

        // actions array (multi-step) with tool_name
        if (element.TryGetObjectProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                if (action.TryGetObjectProperty("tool_name", out _))
                {
                    return true;
                }
            }
        }

        // Cohere V1: tool_calls whose elements are the bare { name, parameters } shape (no
        // "function" wrapper). OpenAI and Cohere V2 wrap each call in "function", so requiring a
        // bare "name" without "function" distinguishes legacy Cohere V1 without stealing OpenAI
        // responses. Without this, a finish_reason-less V1 response falls through to the OpenAI
        // parser, which cannot read { name, parameters } and silently drops the call.
        //
        // Both placements must be checked. V1 nests the array under "message", and that is the
        // shape Parse already reads — recognising only the top-level one left the nested form
        // detectable by no parser at all.
        if (element.TryGetObjectProperty("tool_calls", out var toolCalls) &&
            ContainsBareToolCall(toolCalls))
        {
            return true;
        }

        if (element.TryGetObjectProperty("message", out var v1Message) &&
            v1Message.TryGetObjectProperty("tool_calls", out var messageToolCalls) &&
            ContainsBareToolCall(messageToolCalls))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when the array holds at least one legacy Cohere V1 call: a bare <c>name</c> with no
    /// <c>function</c> wrapper.
    /// </summary>
    private static bool ContainsBareToolCall(JsonElement toolCalls)
    {
        if (toolCalls.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            if (toolCall.TryGetObjectProperty("name", out _) &&
                !toolCall.TryGetObjectProperty("function", out _))
            {
                return true;
            }
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

        // Cohere V2 API: tool_calls array
        if (element.TryGetObjectProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                var parsed = ParseToolCall(toolCall);
                if (parsed != null)
                {
                    results.Add(parsed);
                }
            }
        }

        // Cohere V1 API: tool_plan + tool_calls in message (only if message is an object)
        if (element.TryGetObjectProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetObjectProperty("tool_calls", out var msgToolCalls) && msgToolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCall in msgToolCalls.EnumerateArray())
                {
                    var parsed = ParseToolCall(toolCall);
                    if (parsed != null)
                    {
                        results.Add(parsed);
                    }
                }
            }
        }

        // Check for actions array (multi-step tool use)
        if (element.TryGetObjectProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                var parsed = ParseAction(action);
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
        // Check finish_reason
        if (element.TryGetObjectProperty("finish_reason", out var finishReason))
        {
            var reason = finishReason.GetString();
            if (reason == "TOOL_CALL")
            {
                return true;
            }
        }

        // Check for tool_calls array
        if (element.TryGetObjectProperty("tool_calls", out var toolCalls) &&
            toolCalls.ValueKind == JsonValueKind.Array &&
            toolCalls.GetArrayLength() > 0)
        {
            return true;
        }

        // Check message.tool_calls (only if message is an object)
        if (element.TryGetObjectProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object &&
            message.TryGetObjectProperty("tool_calls", out var msgToolCalls) &&
            msgToolCalls.ValueKind == JsonValueKind.Array &&
            msgToolCalls.GetArrayLength() > 0)
        {
            return true;
        }

        // Check the multi-step actions array. CanParse and Parse both read this surface, so
        // omitting it here made the three disagree: callers following the documented
        // "if (HasToolCalls) Parse" guard skipped an actions response entirely.
        if (element.TryGetObjectProperty("actions", out var actions) &&
            actions.ValueKind == JsonValueKind.Array &&
            actions.GetArrayLength() > 0)
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public string FormatResults(IEnumerable<ToolCallResult> results)
    {
        // Cohere Chat API v2 tool result: a message with role "tool", the tool_call_id
        // that matches the originating call, and the result content. v2 matches results
        // to calls by tool_call_id and does NOT echo the original call parameters (unlike
        // the legacy v1 `tool_results[].call.parameters` shape). This parser parses v2
        // tool calls (see ParseToolCall), so it formats results in the matching v2 shape.
        // https://docs.cohere.com/docs/migrating-v1-to-v2
        var messages = results.Select(r => new
        {
            role = "tool",
            tool_call_id = r.ToolCallId,
            content = r.Content
        });

        return JsonSerializer.Serialize(messages, JsonOptions);
    }

    private static ToolCall? ParseToolCall(JsonElement element)
    {
        // Cohere V2 format: { id, type, function: { name, arguments } }
        if (element.TryGetObjectProperty("function", out var functionElement))
        {
            if (!functionElement.TryGetObjectProperty("name", out var nameElement))
            {
                return null;
            }

            var id = element.TryGetObjectProperty("id", out var idElement)
                ? idElement.GetString() ?? $"call_{Guid.NewGuid():N}"[..29]
                : $"call_{Guid.NewGuid():N}"[..29];

            var arguments = "{}";
            if (functionElement.TryGetObjectProperty("arguments", out var argsElement))
            {
                arguments = argsElement.ValueKind == JsonValueKind.String
                    ? argsElement.GetString() ?? "{}"
                    : argsElement.GetRawText();
            }

            return new ToolCall
            {
                Id = id,
                Name = nameElement.GetString() ?? string.Empty,
                Arguments = arguments
            };
        }

        // Cohere V1 format: { name, parameters }
        if (element.TryGetObjectProperty("name", out var directNameElement))
        {
            var id = $"call_{Guid.NewGuid():N}"[..29];

            var arguments = "{}";
            if (element.TryGetObjectProperty("parameters", out var paramsElement))
            {
                arguments = paramsElement.GetRawText();
            }

            return new ToolCall
            {
                Id = id,
                Name = directNameElement.GetString() ?? string.Empty,
                Arguments = arguments
            };
        }

        return null;
    }

    private static ToolCall? ParseAction(JsonElement element)
    {
        // Multi-step action format: { tool_name, tool_input }
        if (!element.TryGetObjectProperty("tool_name", out var nameElement))
        {
            return null;
        }

        var id = $"call_{Guid.NewGuid():N}"[..29];

        var arguments = "{}";
        if (element.TryGetObjectProperty("tool_input", out var inputElement))
        {
            arguments = inputElement.GetRawText();
        }

        return new ToolCall
        {
            Id = id,
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }
}
