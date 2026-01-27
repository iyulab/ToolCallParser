using System.Text.Json;

namespace ToolCallParser;

/// <summary>
/// Interface for parsing tool calls from LLM responses.
/// </summary>
public interface IToolCallParser
{
    /// <summary>
    /// Gets the provider this parser handles.
    /// </summary>
    Provider Provider { get; }

    /// <summary>
    /// Parses tool calls from a raw JSON response.
    /// </summary>
    /// <param name="response">The raw JSON response from the LLM</param>
    /// <returns>List of parsed tool calls</returns>
    IReadOnlyList<ToolCall> Parse(string response);

    /// <summary>
    /// Parses tool calls from a JsonElement.
    /// </summary>
    /// <param name="element">The JSON element to parse</param>
    /// <returns>List of parsed tool calls</returns>
    IReadOnlyList<ToolCall> Parse(JsonElement element);

    /// <summary>
    /// Checks if the response contains any tool calls.
    /// </summary>
    /// <param name="response">The raw JSON response</param>
    /// <returns>True if tool calls are present</returns>
    bool HasToolCalls(string response);

    /// <summary>
    /// Checks if the JSON element contains any tool calls.
    /// </summary>
    /// <param name="element">The JSON element to check</param>
    /// <returns>True if tool calls are present</returns>
    bool HasToolCalls(JsonElement element);

    /// <summary>
    /// Formats tool call results back to the provider's expected format.
    /// </summary>
    /// <param name="results">The tool call results</param>
    /// <returns>JSON string in the provider's format</returns>
    string FormatResults(IEnumerable<ToolCallResult> results);
}
