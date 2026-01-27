using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

public class OpenAIToolCallParserTests
{
    private readonly OpenAIToolCallParser _parser = new();

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullString_ReturnsEmptyList()
    {
        var result = _parser.Parse((string)null!);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoToolCalls_ReturnsEmptyList()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "content": "Hello!"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleToolCall_ReturnsToolCall()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "tool_calls": [{
                        "id": "call_abc123",
                        "type": "function",
                        "function": {
                            "name": "get_weather",
                            "arguments": "{\"location\": \"Seattle\"}"
                        }
                    }]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("call_abc123", result[0].Id);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Equal("{\"location\": \"Seattle\"}", result[0].Arguments);
    }

    [Fact]
    public void Parse_MultipleToolCalls_ReturnsAllToolCalls()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "tool_calls": [
                        {
                            "id": "call_1",
                            "function": {
                                "name": "tool1",
                                "arguments": "{}"
                            }
                        },
                        {
                            "id": "call_2",
                            "function": {
                                "name": "tool2",
                                "arguments": "{\"x\": 1}"
                            }
                        }
                    ]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("call_1", result[0].Id);
        Assert.Equal("call_2", result[1].Id);
    }

    [Fact]
    public void Parse_DirectToolCallsArray_Works()
    {
        var json = """
        {
            "tool_calls": [{
                "id": "call_direct",
                "function": {
                    "name": "direct_tool",
                    "arguments": "{}"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("call_direct", result[0].Id);
    }

    [Fact]
    public void Parse_LegacyFunctionCall_Works()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "function_call": {
                        "name": "legacy_function",
                        "arguments": "{\"param\": \"value\"}"
                    }
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("legacy_function", result[0].Name);
        Assert.Equal("{\"param\": \"value\"}", result[0].Arguments);
        Assert.StartsWith("call_", result[0].Id); // Generated ID
    }

    [Fact]
    public void HasToolCalls_WithToolCalls_ReturnsTrue()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "tool_calls": [{"id": "1", "function": {"name": "test", "arguments": "{}"}}]
                }
            }]
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithoutToolCalls_ReturnsFalse()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "content": "Just text"
                }
            }]
        }
        """;

        Assert.False(_parser.HasToolCalls(json));
    }

    [Fact]
    public void FormatResults_CreatesCorrectFormat()
    {
        var results = new[]
        {
            ToolCallResult.Success("call_1", "result content", "tool1"),
            ToolCallResult.Failure("call_2", "error message", "tool2")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("\"role\":\"tool\"", formatted);
        Assert.Contains("\"tool_call_id\":\"call_1\"", formatted);
        Assert.Contains("\"content\":\"result content\"", formatted);
    }

    [Fact]
    public void Provider_ReturnsOpenAI()
    {
        Assert.Equal(Provider.OpenAI, _parser.Provider);
    }

    [Fact]
    public void Parse_StreamingDelta_Works()
    {
        var json = """
        {
            "choices": [{
                "delta": {
                    "tool_calls": [{
                        "id": "call_stream",
                        "function": {
                            "name": "stream_tool",
                            "arguments": "{}"
                        }
                    }]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("call_stream", result[0].Id);
    }
}
