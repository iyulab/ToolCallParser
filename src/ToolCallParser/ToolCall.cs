using System.Text.Json;

namespace ToolCallParser;

/// <summary>
/// Represents a normalized tool call that works across all providers.
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// For OpenAI, this is the tool_call id.
    /// For Anthropic, this is generated from the tool_use block id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name of the tool/function being called.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The arguments for the tool call as a JSON string.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// Gets the arguments as a JsonDocument.
    /// </summary>
    public JsonDocument GetArgumentsAsJson()
    {
        return JsonDocument.Parse(Arguments);
    }

    /// <summary>
    /// Gets the arguments deserialized to the specified type.
    /// </summary>
    public T? GetArguments<T>(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(Arguments, options);
    }

    /// <summary>
    /// Gets a specific argument value by name.
    /// </summary>
    public T? GetArgument<T>(string name, JsonSerializerOptions? options = null)
    {
        using var doc = GetArgumentsAsJson();
        if (doc.RootElement.TryGetProperty(name, out var element))
        {
            return element.Deserialize<T>(options);
        }

        return default;
    }

    /// <summary>
    /// Checks if an argument exists.
    /// </summary>
    public bool HasArgument(string name)
    {
        using var doc = GetArgumentsAsJson();
        return doc.RootElement.TryGetProperty(name, out _);
    }
}

/// <summary>
/// Represents a tool call result to send back to the LLM.
/// </summary>
public sealed record ToolCallResult
{
    /// <summary>
    /// The ID of the tool call this result is for.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// The name of the tool that was called.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// The result content (can be any text/JSON).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether the tool execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Optional error message if the tool execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    public static ToolCallResult Success(string toolCallId, string content, string? toolName = null)
    {
        return new ToolCallResult
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            Content = content,
            IsSuccess = true
        };
    }

    /// <summary>
    /// Creates a failed tool result.
    /// </summary>
    public static ToolCallResult Failure(string toolCallId, string errorMessage, string? toolName = null)
    {
        return new ToolCallResult
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            Content = $"Error: {errorMessage}",
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
