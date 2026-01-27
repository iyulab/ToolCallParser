namespace ToolCallParser.Tests;

public class ToolCallParserFactoryTests
{
    [Theory]
    [InlineData(Provider.OpenAI)]
    [InlineData(Provider.AzureOpenAI)]
    [InlineData(Provider.Ollama)]
    [InlineData(Provider.GpuStack)]
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

    [Fact]
    public void GetParser_UnsupportedProvider_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => ToolCallParserFactory.GetParser(Provider.Cohere));
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
    public void RegisterParser_CustomParser_Works()
    {
        var customParser = new CustomTestParser();

        ToolCallParserFactory.RegisterParser(Provider.Mistral, customParser);
        var parser = ToolCallParserFactory.GetParser(Provider.Mistral);

        Assert.Same(customParser, parser);
    }

    private sealed class CustomTestParser : IToolCallParser
    {
        public Provider Provider => Provider.Mistral;

        public IReadOnlyList<ToolCall> Parse(string response) => [];

        public IReadOnlyList<ToolCall> Parse(System.Text.Json.JsonElement element) => [];

        public bool HasToolCalls(string response) => false;

        public bool HasToolCalls(System.Text.Json.JsonElement element) => false;

        public string FormatResults(IEnumerable<ToolCallResult> results) => "[]";
    }
}
