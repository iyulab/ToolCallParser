using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

public class GoogleToolCallParserTests
{
    private readonly GoogleToolCallParser _parser = new();

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
    public void Parse_NoFunctionCall_ReturnsEmptyList()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{"text": "Hello!"}],
                    "role": "model"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_CandidatesFormat_SingleFunctionCall()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "functionCall": {
                            "name": "get_weather",
                            "args": {"location": "Seoul"}
                        }
                    }],
                    "role": "model"
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Contains("Seoul", result[0].Arguments);
        Assert.StartsWith("call_", result[0].Id);
    }

    [Fact]
    public void Parse_CandidatesFormat_MultipleFunctionCalls()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [
                        {
                            "functionCall": {
                                "name": "tool1",
                                "args": {"x": 1}
                            }
                        },
                        {
                            "functionCall": {
                                "name": "tool2",
                                "args": {"y": 2}
                            }
                        }
                    ]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal("tool1", result[0].Name);
        Assert.Equal("tool2", result[1].Name);
    }

    [Fact]
    public void Parse_ContentPartsFormat_Works()
    {
        var json = """
        {
            "content": {
                "parts": [{
                    "functionCall": {
                        "name": "direct_tool",
                        "args": {"key": "value"}
                    }
                }]
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("direct_tool", result[0].Name);
    }

    [Fact]
    public void Parse_PartsOnlyFormat_Works()
    {
        var json = """
        {
            "parts": [{
                "functionCall": {
                    "name": "parts_tool",
                    "args": {}
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("parts_tool", result[0].Name);
    }

    [Fact]
    public void Parse_DirectFunctionCallFormat_Works()
    {
        var json = """
        {
            "functionCall": {
                "name": "inline_tool",
                "args": {"param": "test"}
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("inline_tool", result[0].Name);
        Assert.Contains("test", result[0].Arguments);
    }

    [Fact]
    public void Parse_NoArgs_DefaultsToEmptyObject()
    {
        var json = """
        {
            "functionCall": {
                "name": "no_args_tool"
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("{}", result[0].Arguments);
    }

    [Fact]
    public void Parse_MissingName_SkipsFunctionCall()
    {
        var json = """
        {
            "functionCall": {
                "args": {"key": "value"}
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MixedPartsTextAndFunctionCall_ExtractsFunctionCallOnly()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [
                        {"text": "I will call a function."},
                        {
                            "functionCall": {
                                "name": "search",
                                "args": {"query": "test"}
                            }
                        }
                    ]
                }
            }]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        Assert.Equal("search", result[0].Name);
    }

    [Fact]
    public void Parse_GeneratesUniqueIds()
    {
        var json = """
        {
            "parts": [
                {"functionCall": {"name": "tool1", "args": {}}},
                {"functionCall": {"name": "tool2", "args": {}}}
            ]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].Id, result[1].Id);
        Assert.StartsWith("call_", result[0].Id);
        Assert.StartsWith("call_", result[1].Id);
    }

    [Fact]
    public void HasToolCalls_WithFunctionCall_ReturnsTrue()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "functionCall": {"name": "test", "args": {}}
                    }]
                }
            }]
        }
        """;

        Assert.True(_parser.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithoutFunctionCall_ReturnsFalse()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{"text": "Just text"}]
                }
            }]
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
    public void FormatResults_CreatesFunctionResponseFormat()
    {
        var results = new[]
        {
            ToolCallResult.Success("call_1", "weather data", "get_weather"),
            ToolCallResult.Failure("call_2", "not found", "search")
        };

        var formatted = _parser.FormatResults(results);

        Assert.Contains("functionResponse", formatted);
        Assert.Contains("get_weather", formatted);
        Assert.Contains("weather data", formatted);
        Assert.Contains("search", formatted);
    }

    [Fact]
    public void Provider_ReturnsGoogle()
    {
        Assert.Equal(Provider.Google, _parser.Provider);
    }

    [Fact]
    public void Parse_ComplexNestedArgs_PreservesJson()
    {
        var json = """
        {
            "functionCall": {
                "name": "complex_tool",
                "args": {
                    "nested": {"a": 1, "b": [1, 2, 3]},
                    "array": ["x", "y"],
                    "flag": true
                }
            }
        }
        """;

        var result = _parser.Parse(json);

        Assert.Single(result);
        var args = result[0].GetArguments<Dictionary<string, object>>();
        Assert.NotNull(args);
    }
}
