using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

public class BedrockToolCallParserTests
{
    private readonly BedrockToolCallParser _parser = new();

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
    public void Parse_NoToolUse_ReturnsEmptyList()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [{"text": "Hello!"}],
                    "role": "assistant"
                }
            },
            "stopReason": "end_turn"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    #region Converse API Response Format

    [Fact]
    public void Parse_ConverseApiFormat_SingleToolUse()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [{
                        "toolUse": {
                            "toolUseId": "tooluse_abc123",
                            "name": "get_weather",
                            "input": {"location": "Tokyo"}
                        }
                    }],
                    "role": "assistant"
                }
            },
            "stopReason": "tool_use"
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("tooluse_abc123", result[0].Id);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Contains("Tokyo", result[0].Arguments);
    }

    [Fact]
    public void Parse_ConverseApiFormat_MultipleToolUse()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [
                        {
                            "toolUse": {
                                "toolUseId": "tooluse_1",
                                "name": "tool1",
                                "input": {"x": 1}
                            }
                        },
                        {"text": "I'll call two tools."},
                        {
                            "toolUse": {
                                "toolUseId": "tooluse_2",
                                "name": "tool2",
                                "input": {"y": 2}
                            }
                        }
                    ]
                }
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("tooluse_1", result[0].Id);
        Assert.Equal("tooluse_2", result[1].Id);
    }

    #endregion

    #region Message Format

    [Fact]
    public void Parse_MessageFormat_Works()
    {
        var json = """
        {
            "message": {
                "content": [{
                    "toolUse": {
                        "toolUseId": "tooluse_msg",
                        "name": "msg_tool",
                        "input": {"key": "value"}
                    }
                }]
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("msg_tool", result[0].Name);
    }

    [Fact]
    public void Parse_MessageNotObject_Ignores()
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

    #region Direct Content Format

    [Fact]
    public void Parse_DirectContentFormat_Works()
    {
        var json = """
        {
            "content": [{
                "toolUse": {
                    "toolUseId": "tooluse_direct",
                    "name": "direct_tool",
                    "input": {}
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("direct_tool", result[0].Name);
    }

    [Fact]
    public void Parse_DirectToolUseFormat_Works()
    {
        var json = """
        {
            "toolUse": {
                "toolUseId": "tooluse_inline",
                "name": "inline_tool",
                "input": {"param": "test"}
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("inline_tool", result[0].Name);
        Assert.Contains("test", result[0].Arguments);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_NoToolUseId_GeneratesId()
    {
        var json = """
        {
            "toolUse": {
                "name": "no_id_tool",
                "input": {}
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.StartsWith("tooluse_", result[0].Id);
    }

    [Fact]
    public void Parse_MissingName_Skips()
    {
        var json = """
        {
            "toolUse": {
                "toolUseId": "tooluse_bad",
                "input": {"key": "value"}
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NoInput_DefaultsToEmptyObject()
    {
        var json = """
        {
            "toolUse": {
                "toolUseId": "tooluse_noinput",
                "name": "no_input_tool"
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("{}", result[0].Arguments);
    }

    #endregion

    #region HasToolCalls

    [Fact]
    public void HasToolCalls_StopReasonToolUse_ReturnsTrue()
    {
        var json = """
        {
            "stopReason": "tool_use",
            "output": {
                "message": {
                    "content": [{"toolUse": {"name": "test", "input": {}}}]
                }
            }
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_ToolUseBlocks_ReturnsTrue()
    {
        var json = """
        {
            "content": [{
                "toolUse": {
                    "name": "test",
                    "input": {}
                }
            }]
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_NoToolUse_ReturnsFalse()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [{"text": "Just text"}]
                }
            },
            "stopReason": "end_turn"
        }
        """;

        Assert.False(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_EmptyString_ReturnsFalse()
    {
        Assert.False(_parser.HasToolCalls(string.Empty));
    }

    #endregion

    #region FormatResults

    [Fact]
    public void FormatResults_JsonContent_PassesThrough()
    {
        var results = new[]
        {
            ToolCallResult.Success("tooluse_1", """{"data": "value"}""", "tool1")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("toolResult", formatted);
        Assert.Contains("tooluse_1", formatted);
        Assert.Contains("\"status\":\"success\"", formatted);
    }

    [Fact]
    public void FormatResults_PlainTextContent_WrapsInJson()
    {
        var results = new[]
        {
            ToolCallResult.Success("tooluse_2", "plain text result", "tool2")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("toolResult", formatted);
        Assert.Contains("plain text result", formatted);
        Assert.Contains("\"role\":\"user\"", formatted);
    }

    [Fact]
    public void FormatResults_ErrorResult_SetsErrorStatus()
    {
        var results = new[]
        {
            ToolCallResult.Failure("tooluse_err", "something failed", "broken_tool")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("\"status\":\"error\"", formatted);
        Assert.Contains("something failed", formatted);
    }

    #endregion

    [Fact]
    public void Provider_ReturnsBedrock()
    {
        Assert.Equal(Provider.Bedrock, _parser.Provider);
    }
}
