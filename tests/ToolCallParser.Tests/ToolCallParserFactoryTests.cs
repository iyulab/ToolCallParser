namespace ToolCallParser.Tests;

public class ToolCallParserFactoryTests
{
    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.AzureOpenAI)]
    [InlineData(Provider.OpenAICompatible)]
    [InlineData(Provider.Anthropic)]
    public void GetParser_SupportedProvider_ReturnsParser(Provider provider)
    {
        var parser = ToolCallParserFactory.GetParser(provider);

        Assert.NotNull(parser);
    }

    [Fact]
    public void GetParser_Auto_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ToolCallParserFactory.GetParser(Provider.Auto));
    }

    [Theory]
    [InlineData(Provider.Google)]
    [InlineData(Provider.Cohere)]
    [InlineData(Provider.Bedrock)]
    [InlineData(Provider.XAI)]
    [InlineData(Provider.DeepSeek)]
    public void GetParser_NewProviders_ReturnsParser(Provider provider)
    {
        var parser = ToolCallParserFactory.GetParser(provider);

        Assert.NotNull(parser);
    }

    [Fact]
    public void DetectProvider_OpenAIFormat_ReturnsOpenAI()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "tool_calls": []
                }
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.OpenAI, provider);
    }

    [Fact]
    public void DetectProvider_AnthropicFormat_ReturnsAnthropic()
    {
        var json = """
        {
            "content": [{"type": "text", "text": "Hello"}],
            "stop_reason": "end_turn"
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Anthropic, provider);
    }

    [Fact]
    public void DetectProvider_UnknownFormat_ReturnsAuto()
    {
        var json = """{"data": "unknown format"}""";

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Auto, provider);
    }

    [Fact]
    public void Parse_OpenAIResponse_AutoDetects()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "tool_calls": [{
                        "id": "call_1",
                        "function": {"name": "test", "arguments": "{}"}
                    }]
                }
            }]
        }
        """;

        var result = ToolCallParserFactory.Parse(json);

        Assert.Single(result);
        Assert.Equal("call_1", result[0].Id);
    }

    [Fact]
    public void Parse_AnthropicResponse_AutoDetects()
    {
        var json = """
        {
            "content": [{
                "type": "tool_use",
                "id": "toolu_1",
                "name": "test",
                "input": {}
            }],
            "stop_reason": "tool_use"
        }
        """;

        var result = ToolCallParserFactory.Parse(json);

        Assert.Single(result);
        Assert.Equal("toolu_1", result[0].Id);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var result = ToolCallParserFactory.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void HasToolCalls_WithOpenAIToolCalls_ReturnsTrue()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "tool_calls": [{"id": "1", "function": {"name": "x", "arguments": "{}"}}]
                }
            }]
        }
        """;

        Assert.True(ToolCallParserFactory.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithAnthropicToolUse_ReturnsTrue()
    {
        var json = """
        {
            "content": [{"type": "tool_use", "id": "1", "name": "x", "input": {}}]
        }
        """;

        Assert.True(ToolCallParserFactory.HasToolCalls(json));
    }

    [Fact]
    public void HasToolCalls_WithoutToolCalls_ReturnsFalse()
    {
        var json = """{"message": "no tools here"}""";

        Assert.False(ToolCallParserFactory.HasToolCalls(json));
    }

    [Fact]
    public void Parse_NullString_ReturnsEmpty()
    {
        var result = ToolCallParserFactory.Parse((string)null!);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmpty()
    {
        var result = ToolCallParserFactory.Parse("   ");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ToolCallParserFactory.Parse("not json"));
    }

    [Fact]
    public void DetectProvider_NullOrEmpty_ReturnsAuto()
    {
        Assert.Equal(Provider.Auto, ToolCallParserFactory.DetectProvider(string.Empty));
        Assert.Equal(Provider.Auto, ToolCallParserFactory.DetectProvider((string)null!));
        Assert.Equal(Provider.Auto, ToolCallParserFactory.DetectProvider("  "));
    }

    [Fact]
    public void DetectProvider_GoogleFormat_ReturnsGoogle()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "functionCall": {
                            "name": "get_weather",
                            "args": {"location": "Seattle"}
                        }
                    }]
                }
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Google, provider);
    }

    [Fact]
    public void DetectProvider_GoogleDirectFunctionCall_ReturnsGoogle()
    {
        var json = """
        {
            "functionCall": {
                "name": "test",
                "args": {}
            }
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Google, provider);
    }

    [Fact]
    public void DetectProvider_BedrockFormat_ReturnsBedrock()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [{
                        "toolUse": {
                            "toolUseId": "tu_1",
                            "name": "test",
                            "input": {}
                        }
                    }]
                }
            },
            "stopReason": "tool_use"
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Bedrock, provider);
    }

    [Fact]
    public void DetectProvider_CohereFormat_ReturnsCohere()
    {
        var json = """
        {
            "finish_reason": "TOOL_CALL",
            "tool_calls": [{
                "name": "test",
                "parameters": {}
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Cohere, provider);
    }

    [Fact]
    public void DetectProvider_CohereToolPlan_ReturnsCohere()
    {
        var json = """
        {
            "tool_plan": "I will search for the user.",
            "tool_calls": [{
                "name": "search_user",
                "parameters": {"query": "John"}
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Cohere, provider);
    }

    [Fact]
    public void DetectProvider_CohereV1BareToolCalls_ReturnsCohere()
    {
        // Legacy Cohere V1: top-level tool_calls with bare {name, parameters}, no finish_reason/
        // tool_plan. Must route to Cohere (not OpenAI, whose parser cannot read this shape).
        var json = """
        {
            "finish_reason": "COMPLETE",
            "tool_calls": [{
                "name": "get_weather",
                "parameters": {"location": "Toronto"}
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Cohere, provider);
    }

    [Fact]
    public void Parse_CohereV1BareToolCalls_ExtractsCall()
    {
        // Regression: the finish_reason-less V1 shape must be auto-detected AND parsed, not
        // silently dropped by misrouting to the OpenAI parser.
        var json = """
        {
            "finish_reason": "COMPLETE",
            "tool_calls": [{
                "name": "get_weather",
                "parameters": {"location": "Toronto"}
            }]
        }
        """;

        var result = ToolCallParserFactory.Parse(json);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].Name);
        Assert.Contains("Toronto", result[0].Arguments);
    }

    [Fact]
    public void DetectProvider_OpenAIFunctionWrappedToolCalls_NotStolenByCohere()
    {
        // Guard: OpenAI/V2 tool_calls wrap the call in "function"; the new Cohere V1 bare-name
        // detection must NOT claim these. Cohere is probed before OpenAI, so this proves no theft.
        var json = """
        {
            "tool_calls": [{
                "id": "call_1",
                "type": "function",
                "function": {"name": "get_weather", "arguments": "{\"location\":\"Toronto\"}"}
            }]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.OpenAI, provider);
    }

    [Fact]
    public void DetectProvider_AnthropicContentToolUse_ReturnsAnthropic()
    {
        // Anthropic format detected by content array with tool_use type (without stop_reason)
        var json = """
        {
            "content": [{"type": "tool_use", "id": "toolu_1", "name": "test", "input": {}}]
        }
        """;

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.Anthropic, provider);
    }

    [Fact]
    public void DetectProvider_OpenAIToolCallsDirect_ReturnsOpenAI()
    {
        var json = """{"tool_calls": [{"id": "1", "function": {"name": "x", "arguments": "{}"}}]}""";

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.OpenAI, provider);
    }

    [Fact]
    public void DetectProvider_OpenAIFunctionCall_ReturnsOpenAI()
    {
        var json = """{"function_call": {"name": "test", "arguments": "{}"}}""";

        var provider = ToolCallParserFactory.DetectProvider(json);

        Assert.Equal(Provider.OpenAI, provider);
    }

    [Fact]
    public void TryGetParser_SupportedProvider_ReturnsTrue()
    {
        var found = ToolCallParserFactory.TryGetParser(Provider.OpenAI, out var parser);

        Assert.True(found);
        Assert.NotNull(parser);
    }

    [Fact]
    public void TryGetParser_Auto_ReturnsFalse()
    {
        var found = ToolCallParserFactory.TryGetParser(Provider.Auto, out var parser);

        Assert.False(found);
        Assert.Null(parser);
    }

    [Fact]
    public void GetRegisteredProviders_ContainsAllExpectedProviders()
    {
        var providers = ToolCallParserFactory.GetRegisteredProviders().ToList();

        Assert.Contains(Provider.OpenAI, providers);
        Assert.Contains(Provider.Anthropic, providers);
        Assert.Contains(Provider.Google, providers);
        Assert.Contains(Provider.Cohere, providers);
        Assert.Contains(Provider.Bedrock, providers);
        Assert.Contains(Provider.AzureOpenAI, providers);
        Assert.DoesNotContain(Provider.Auto, providers);
    }

    [Fact]
    public void GetOpenAICompatibleProviders_ContainsExpectedProviders()
    {
        var providers = ToolCallParserFactory.GetOpenAICompatibleProviders().ToList();

        Assert.Contains(Provider.OpenAI, providers);
        Assert.Contains(Provider.AzureOpenAI, providers);
        Assert.Contains(Provider.Mistral, providers);
        Assert.Contains(Provider.DeepSeek, providers);
        Assert.Contains(Provider.OpenAICompatible, providers);
        Assert.DoesNotContain(Provider.Anthropic, providers);
        Assert.DoesNotContain(Provider.Google, providers);
    }

    [Fact]
    public void HasToolCalls_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(ToolCallParserFactory.HasToolCalls(string.Empty));
        Assert.False(ToolCallParserFactory.HasToolCalls((string)null!));
        Assert.False(ToolCallParserFactory.HasToolCalls("  "));
    }

    [Fact]
    public void HasToolCalls_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ToolCallParserFactory.HasToolCalls("not json"));
    }

    [Fact]
    public void Parse_GoogleResponse_AutoDetects()
    {
        var json = """
        {
            "candidates": [{
                "content": {
                    "parts": [{
                        "functionCall": {
                            "name": "get_weather",
                            "args": {"location": "Seattle"}
                        }
                    }]
                }
            }]
        }
        """;

        var result = ToolCallParserFactory.Parse(json);

        Assert.Single(result);
        Assert.Equal("get_weather", result[0].Name);
    }

    [Fact]
    public void Parse_BedrockResponse_AutoDetects()
    {
        var json = """
        {
            "output": {
                "message": {
                    "content": [{
                        "toolUse": {
                            "toolUseId": "tu_123",
                            "name": "get_info",
                            "input": {"key": "val"}
                        }
                    }]
                }
            },
            "stopReason": "tool_use"
        }
        """;

        var result = ToolCallParserFactory.Parse(json);

        Assert.Single(result);
        Assert.Equal("get_info", result[0].Name);
    }

    [Fact]
    public void Parse_UnknownFormat_FallbackReturnsEmpty()
    {
        var json = """{"data": "no tool calls in any format"}""";

        var result = ToolCallParserFactory.Parse(json);

        Assert.Empty(result);
    }
}
