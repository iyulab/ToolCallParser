using System.Text.Json;

namespace ToolCallParser.Parsers;

/// <summary>
/// Parser for Anthropic Claude-style tool calls.
/// Handles tool_use content blocks in Claude's messages API format.
/// </summary>
public sealed class AnthropicToolCallParser : IToolCallParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc />
    public Provider Provider => Provider.Anthropic;

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

        // Find content array
        if (!TryGetContentArray(element, out var contentArray))
        {
            return results;
        }

        foreach (var contentBlock in contentArray.EnumerateArray())
        {
            if (IsToolUseBlock(contentBlock))
            {
                var parsed = ParseToolUseBlock(contentBlock);
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
        // Check stop_reason first (fast path)
        if (element.TryGetProperty("stop_reason", out var stopReason))
        {
            var reason = stopReason.GetString();
            if (reason == "tool_use")
            {
                return true;
            }
        }

        // Check content blocks
        if (!TryGetContentArray(element, out var contentArray))
        {
            return false;
        }

        foreach (var contentBlock in contentArray.EnumerateArray())
        {
            if (IsToolUseBlock(contentBlock))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public string FormatResults(IEnumerable<ToolCallResult> results)
    {
        var contentBlocks = results.Select(r => new
        {
            type = "tool_result",
            tool_use_id = r.ToolCallId,
            content = r.Content,
            is_error = !r.IsSuccess
        });

        var message = new
        {
            role = "user",
            content = contentBlocks
        };

        return JsonSerializer.Serialize(message, JsonOptions);
    }

    private static bool TryGetContentArray(JsonElement element, out JsonElement contentArray)
    {
        contentArray = default;

        // Direct content array
        if (element.TryGetProperty("content", out contentArray) && contentArray.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        return false;
    }

    private static bool IsToolUseBlock(JsonElement block)
    {
        if (!block.TryGetProperty("type", out var typeElement))
        {
            return false;
        }

        return typeElement.GetString() == "tool_use";
    }

    private static ToolCall? ParseToolUseBlock(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var arguments = "{}";
        if (element.TryGetProperty("input", out var inputElement))
        {
            arguments = inputElement.GetRawText();
        }

        return new ToolCall
        {
            Id = idElement.GetString() ?? string.Empty,
            Name = nameElement.GetString() ?? string.Empty,
            Arguments = arguments
        };
    }
}
