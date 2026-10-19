using Xunit;

namespace ToolCallParser.Tests;

/// <summary>
/// Detection, the presence check and parsing give the same answer for one input, and the documented error contract
/// holds: text that is not JSON has no tool calls; a call whose arguments are not a JSON object is a
/// <see cref="FormatException"/> naming the tool.
/// </summary>
public class ContractConsistencyTests
{
    private sealed record WeatherArgs(string? Location);

    [Fact]
    public void OllamaNativeResponse_ParsesTheCallItReportsHaving()
    {
        const string json = """{"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"get_weather","arguments":{"location":"Seoul"}}}]},"done":true}""";

        Assert.True(ToolCallParserFactory.HasToolCalls(json));
        var call = Assert.Single(ToolCallParserFactory.Parse(json));
        Assert.Equal("get_weather", call.Name);
        Assert.False(string.IsNullOrEmpty(call.Id));
        Assert.Equal("Seoul", call.GetArgument<string>("location"));
    }

    [Fact]
    public void ArgumentsGivenAsAnObject_AreRead()
    {
        const string json = """{"choices":[{"message":{"tool_calls":[{"id":"c1","type":"function","function":{"name":"get_weather","arguments":{"location":"Tokyo"}}}]}}]}""";

        Assert.Equal("Tokyo", Assert.Single(ToolCallParserFactory.Parse(json)).GetArgument<string>("location"));
    }

    [Fact]
    public void GetArguments_BindsCamelCaseJsonToPascalCaseProperties()
    {
        var call = new ToolCall { Id = "c1", Name = "get_weather", Arguments = """{"location":"Tokyo"}""" };

        Assert.Equal("Tokyo", call.GetArguments<WeatherArgs>()!.Location);
    }

    [Theory]
    [InlineData("The weather in Tokyo is sunny.")]
    [InlineData("{ not json")]
    [InlineData("")]
    public void TextThatIsNotJson_HasNoToolCalls(string text)
    {
        Assert.False(ToolCallParserFactory.HasToolCalls(text));
        Assert.Empty(ToolCallParserFactory.Parse(text));
        Assert.Equal(Provider.Auto, ToolCallParserFactory.DetectProvider(text));
        foreach (var provider in new[] { Provider.OpenAI, Provider.Anthropic, Provider.Google, Provider.Cohere, Provider.Bedrock })
        {
            var parser = ToolCallParserFactory.GetParser(provider);
            Assert.False(parser.HasToolCalls(text));
            Assert.Empty(parser.Parse(text));
        }
    }

    [Theory]
    [InlineData("""{"a":1,}""")]
    [InlineData("[1, 2]")]
    [InlineData("not json")]
    public void ArgumentsThatAreNotAJsonObject_AreAFormatExceptionNamingTheTool(string arguments)
    {
        var encoded = System.Text.Json.JsonSerializer.Serialize(arguments);
        var json = """{"choices":[{"message":{"tool_calls":[{"id":"c1","type":"function","function":{"name":"get_weather","arguments":""" + encoded + "}}]}}]}";

        var ex = Assert.Throws<FormatException>(() => ToolCallParserFactory.Parse(json));
        Assert.Contains("get_weather", ex.Message);
    }

    [Theory]
    [InlineData("""{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"c1","type":"function","function":{"name":"get_weather","arguments":""}}]}}]}""")]
    [InlineData("""{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"loc"}}]}}]}""")]
    public void AStreamingFragment_IsNotACall(string chunk)
    {
        Assert.False(ToolCallParserFactory.HasToolCalls(chunk));
        Assert.Empty(ToolCallParserFactory.Parse(chunk));
    }

    [Fact]
    public void AStreamingChunkHoldingAWholeCall_IsACall()
    {
        const string chunk = """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"c1","type":"function","function":{"name":"get_weather","arguments":"{\"location\":\"Tokyo\"}"}}]}}]}""";

        Assert.True(ToolCallParserFactory.HasToolCalls(chunk));
        Assert.Equal("c1", Assert.Single(ToolCallParserFactory.Parse(chunk)).Id);
    }
}
