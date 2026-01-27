using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

public class AnthropicToolCallParserTests
{
    private readonly AnthropicToolCallParser _parser = new();

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoToolUse_ReturnsEmptyList()
    {
        var json = """
        {
            "content": [
                {
                    "type": "text",
                    "text": "Hello!"
                }
            ],
            "stop_reason": "end_turn"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleToolUse_ReturnsToolCall()
    {
        var json = """
        {
            "content": [
                {
                    "type": "tool_use",
                    "id": "toolu_01abc",
                    "name": "get_weather",
                    "input": {"location": "Seattle"}
                }
            ],
            "stop_reason": "tool_use"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("toolu_01abc", result[0].Id);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Contains("Seattle", result[0].Arguments);
    }

    [Fact]
    public void Parse_MultipleToolUse_ReturnsAllToolCalls()
    {
        var json = """
        {
            "content": [
                {
                    "type": "text",
                    "text": "I'll use two tools."
                },
                {
                    "type": "tool_use",
                    "id": "toolu_1",
                    "name": "tool1",
                    "input": {}
                },
                {
                    "type": "tool_use",
                    "id": "toolu_2",
                    "name": "tool2",
                    "input": {"x": 1}
                }
            ],
            "stop_reason": "tool_use"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("toolu_1", result[0].Id);
        Assert.Equal("toolu_2", result[1].Id);
    }

    [Fact]
    public void Parse_MixedContent_ExtractsOnlyToolUse()
    {
        var json = """
        {
            "content": [
                {"type": "text", "text": "Let me help."},
                {"type": "tool_use", "id": "toolu_abc", "name": "helper", "input": {}},
                {"type": "text", "text": "Done."}
            ]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("helper", result[0].Name);
    }

    [Fact]
    public void HasToolCalls_WithToolUse_ReturnsTrue()
    {
        var json = """
        {
            "content": [{"type": "tool_use", "id": "1", "name": "test", "input": {}}],
            "stop_reason": "tool_use"
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithStopReasonToolUse_ReturnsTrue()
    {
        var json = """
        {
            "content": [{"type": "tool_use", "id": "x", "name": "y", "input": {}}],
            "stop_reason": "tool_use"
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithoutToolUse_ReturnsFalse()
    {
        var json = """
        {
            "content": [{"type": "text", "text": "Hello"}],
            "stop_reason": "end_turn"
        }
        """;

        Assert.False(_parser.HasToolCalls(json));
    }

    [Fact]
    public void FormatResults_CreatesCorrectFormat()
    {
        var results = new[]
        {
            ToolCallResult.Success("toolu_1", "result content"),
            ToolCallResult.Failure("toolu_2", "error message")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("\"role\":\"user\"", formatted);
        Assert.Contains("\"type\":\"tool_result\"", formatted);
        Assert.Contains("\"tool_use_id\":\"toolu_1\"", formatted);
        Assert.Contains("\"is_error\":false", formatted);
        Assert.Contains("\"is_error\":true", formatted);
    }

    [Fact]
    public void Provider_ReturnsAnthropic()
    {
        Assert.Equal(Provider.Anthropic, _parser.Provider);
    }

    [Fact]
    public void Parse_ComplexInput_PreservesJson()
    {
        var json = """
        {
            "content": [{
                "type": "tool_use",
                "id": "toolu_complex",
                "name": "complex_tool",
                "input": {
                    "nested": {"a": 1, "b": [1, 2, 3]},
                    "array": ["x", "y"]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        var args = result[0].GetArguments<Dictionary<string, object>>();
        Assert.NotNull(args);
    }
}
