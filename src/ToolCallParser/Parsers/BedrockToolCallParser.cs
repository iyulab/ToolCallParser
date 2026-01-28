using System.Text.Json;

namespace ToolCallParser.Parsers;

/// <summary>
/// Parser for AWS Bedrock Converse API tool calls.
/// Handles toolUse content blocks in Bedrock's responses.
///
/// Documentation: https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html
/// </summary>
public sealed class BedrockToolCallParser : IToolCallParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public Provider Provider => Provider.Bedrock;

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

        // Find toolUse blocks in content
        if (TryGetToolUseBlocks(element, out var toolUseBlocks))
        {
            foreach (var toolUse in toolUseBlocks)
            {
                var parsed = ParseToolUse(toolUse);
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
        // Check stopReason
        if (element.TryGetProperty("stopReason", out var stopReason))
        {
            var reason = stopReason.GetString();
            if (reason == "tool_use")
            {
                return true;
            }
        }

        return TryGetToolUseBlocks(element, out var blocks) && blocks.Count > 0;
    }

    /// <inheritdoc />
    public string FormatResults(IEnumerable<ToolCallResult> results)
    {
        // Bedrock expects toolResult format
        var content = results.Select(r => new
        {
            toolResult = new
            {
                toolUseId = r.ToolCallId,
                content = new[]
                {
                    new
                    {
                        json = JsonSerializer.Deserialize<object>(
                            r.Content.StartsWith('{') || r.Content.StartsWith('[')
                                ? r.Content
                                : JsonSerializer.Serialize(new { result = r.Content }))
                    }
                },
                status = r.IsSuccess ? "success" : "error"
            }
        });

        var message = new
        {
            role = "user",
            content
        };

        return JsonSerializer.Serialize(message, JsonOptions);
    }

    private static bool TryGetToolUseBlocks(JsonElement element, out List<JsonElement> toolUseBlocks)
    {
        toolUseBlocks = [];

        // Check output.message.content[].toolUse (Converse API response format)
        if (element.TryGetProperty("output", out var output) &&
            output.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("toolUse", out var toolUse))
                {
                    toolUseBlocks.Add(toolUse);
                }
            }
        }

        // Check message.content[].toolUse (message format, only if message is an object)
        if (element.TryGetProperty("message", out var directMessage) &&
            directMessage.ValueKind == JsonValueKind.Object &&
            directMessage.TryGetProperty("content", out var directContent) &&
            directContent.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in directContent.EnumerateArray())
            {
                if (block.TryGetProperty("toolUse", out var toolUse))
                {
                    toolUseBlocks.Add(toolUse);
                }
            }
        }

        // Check content[].toolUse directly
        if (element.TryGetProperty("content", out var contentOnly) && contentOnly.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentOnly.EnumerateArray())
            {
                if (block.TryGetProperty("toolUse", out var toolUse))
                {
                    toolUseBlocks.Add(toolUse);
                }
            }
        }

        // Check direct toolUse
        if (element.TryGetProperty("toolUse", out var directToolUse))
        {
            toolUseBlocks.Add(directToolUse);
        }

        return toolUseBlocks.Count > 0;
    }

    private static ToolCall? ParseToolUse(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameElement))
        {
            return null;
        }

        var id = element.TryGetProperty("toolUseId", out var idElement)
            ? idElement.GetString() ?? $"tooluse_{Guid.NewGuid():N}"[..29]
            : $"tooluse_{Guid.NewGuid():N}"[..29];

        var arguments = "{}";
        if (element.TryGetProperty("input", out var inputElement))
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
