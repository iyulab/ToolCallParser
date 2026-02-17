using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

public class CohereToolCallParserTests
{
    private readonly CohereToolCallParser _parser = new();

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
            "text": "Hello!",
            "finish_reason": "COMPLETE"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    #region V2 API Format

    [Fact]
    public void Parse_V2Format_SingleToolCall()
    {
        var json = """
        {
            "tool_calls": [{
                "id": "call_v2_1",
                "type": "function",
                "function": {
                    "name": "get_weather",
                    "arguments": "{\"location\": \"Seoul\"}"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("call_v2_1", result[0].Id);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Contains("Seoul", result[0].Arguments);
    }

    [Fact]
    public void Parse_V2Format_MultipleToolCalls()
    {
        var json = """
        {
            "tool_calls": [
                {
                    "id": "call_1",
                    "function": {
                        "name": "tool1",
                        "arguments": "{\"x\": 1}"
                    }
                },
                {
                    "id": "call_2",
                    "function": {
                        "name": "tool2",
                        "arguments": "{\"y\": 2}"
                    }
                }
            ]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("call_1", result[0].Id);
        Assert.Equal("call_2", result[1].Id);
    }

    [Fact]
    public void Parse_V2Format_ArgumentsAsObject()
    {
        var json = """
        {
            "tool_calls": [{
                "id": "call_obj",
                "function": {
                    "name": "tool",
                    "arguments": {"key": "value", "num": 42}
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Contains("key", result[0].Arguments);
        Assert.Contains("42", result[0].Arguments);
    }

    [Fact]
    public void Parse_V2Format_NoId_GeneratesId()
    {
        var json = """
        {
            "tool_calls": [{
                "function": {
                    "name": "no_id_tool",
                    "arguments": "{}"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.StartsWith("call_", result[0].Id);
        Assert.Equal("no_id_tool", result[0].Name);
    }

    [Fact]
    public void Parse_V2Format_MissingFunctionName_Skips()
    {
        var json = """
        {
            "tool_calls": [{
                "id": "call_bad",
                "function": {
                    "arguments": "{}"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    #endregion

    #region V1 API Format

    [Fact]
    public void Parse_V1Format_MessageToolCalls()
    {
        var json = """
        {
            "message": {
                "tool_calls": [{
                    "name": "search",
                    "parameters": {"query": "test"}
                }]
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("search", result[0].Name);
        Assert.Contains("test", result[0].Arguments);
        Assert.StartsWith("call_", result[0].Id);
    }

    [Fact]
    public void Parse_V1Format_NoParameters_DefaultsToEmpty()
    {
        var json = """
        {
            "message": {
                "tool_calls": [{
                    "name": "no_params_tool"
                }]
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("{}", result[0].Arguments);
    }

    [Fact]
    public void Parse_V1Format_MessageNotObject_Ignores()
    {
        var json = """
        {
            "message": "just a string"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    #endregion

    #region Multi-step Actions Format

    [Fact]
    public void Parse_ActionsFormat_SingleAction()
    {
        var json = """
        {
            "actions": [{
                "tool_name": "web_search",
                "tool_input": {"query": "latest news"}
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("web_search", result[0].Name);
        Assert.Contains("latest news", result[0].Arguments);
    }

    [Fact]
    public void Parse_ActionsFormat_MultipleActions()
    {
        var json = """
        {
            "actions": [
                {"tool_name": "action1", "tool_input": {"a": 1}},
                {"tool_name": "action2", "tool_input": {"b": 2}}
            ]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("action1", result[0].Name);
        Assert.Equal("action2", result[1].Name);
    }

    [Fact]
    public void Parse_ActionsFormat_MissingToolName_Skips()
    {
        var json = """
        {
            "actions": [{
                "tool_input": {"key": "value"}
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ActionsFormat_NoToolInput_DefaultsToEmpty()
    {
        var json = """
        {
            "actions": [{
                "tool_name": "simple_action"
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("{}", result[0].Arguments);
    }

    #endregion

    #region HasToolCalls

    [Fact]
    public void HasToolCalls_FinishReasonToolCall_ReturnsTrue()
    {
        var json = """
        {
            "finish_reason": "TOOL_CALL"
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_ToolCallsArray_ReturnsTrue()
    {
        var json = """
        {
            "tool_calls": [{"function": {"name": "test"}}]
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_MessageToolCalls_ReturnsTrue()
    {
        var json = """
        {
            "message": {
                "tool_calls": [{"name": "test"}]
            }
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_EmptyToolCalls_ReturnsFalse()
    {
        var json = """
        {
            "tool_calls": [],
            "finish_reason": "COMPLETE"
        }
        """;

        Assert.False(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_EmptyString_ReturnsFalse()
    {
        Assert.False(_parser.HasToolCalls(string.Empty));
    }

    [Fact]
    public void HasToolCalls_NoToolIndicators_ReturnsFalse()
    {
        var json = """
        {
            "text": "Just a response",
            "finish_reason": "COMPLETE"
        }
        """;

        Assert.False(_parser.HasToolCalls(json));
    }

    #endregion

    #region FormatResults

    [Fact]
    public void FormatResults_CreatesToolResultsFormat()
    {
        var results = new[]
        {
            ToolCallResult.Success("call_1", "weather data", "get_weather"),
            ToolCallResult.Failure("call_2", "error", "search")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("tool_results", formatted);
        Assert.Contains("get_weather", formatted);
        Assert.Contains("weather data", formatted);
        Assert.Contains("outputs", formatted);
    }

    #endregion

    [Fact]
    public void Provider_ReturnsCohere()
    {
        Assert.Equal(Provider.Cohere, _parser.Provider);
    }
}
