using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

/// <summary>
/// Responses API function-call items ({"type":"function_call","call_id":...,"name":...,"arguments":...}).
/// Models exclusively served through the Responses API (e.g. gpt-5.4-pro) emit this shape
/// instead of Chat Completions tool_calls. Format source: developers.openai.com function-calling guide.
/// </summary>
public class OpenAIResponsesApiParserTests
{
    private readonly OpenAIToolCallParser _parser = new();

    private const string ResponsesOutputJson = """
    {
        "id": "resp_123",
        "output": [
            {
                "id": "fc_12345xyz",
                "call_id": "call_12345xyz",
                "type": "function_call",
                "name": "get_weather",
                "arguments": "{\"location\":\"Paris, France\"}"
            },
            {
                "type": "message",
                "content": [{ "type": "output_text", "text": "checking..." }]
            }
        ]
    }
    """;

    [Fact]
    public void CanParse_ResponsesOutputArray_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse(ResponsesOutputJson);

        Assert.True(_parser.CanParse(doc.RootElement));
    }

    [Fact]
    public void Parse_ResponsesOutputArray_ExtractsFunctionCall()
    {
        var result = _parser.Parse(ResponsesOutputJson);

        var call = Assert.Single(result);
        Assert.Equal("call_12345xyz", call.Id);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("""{"location":"Paris, France"}""", call.Arguments);
    }

    [Fact]
    public void Parse_BareFunctionCallItem_ExtractsCall()
    {
        var json = """
        {
            "id": "fc_1",
            "call_id": "call_1",
            "type": "function_call",
            "name": "lookup",
            "arguments": "{\"q\":\"x\"}"
        }
        """;

        var result = _parser.Parse(json);

        var call = Assert.Single(result);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("lookup", call.Name);
    }

    [Fact]
    public void Parse_ItemWithoutCallId_FallsBackToItemId()
    {
        var json = """
        { "id": "fc_only", "type": "function_call", "name": "f", "arguments": "{}" }
        """;

        var result = _parser.Parse(json);

        Assert.Equal("fc_only", Assert.Single(result).Id);
    }

    [Fact]
    public void Parse_ObjectArguments_SerializedAsJson()
    {
        // Adjacent item-style formats carry arguments as an object; tolerate both.
        var json = """
        { "type": "function_call", "name": "set_light", "arguments": {"brightness": 25} }
        """;

        var result = _parser.Parse(json);

        var call = Assert.Single(result);
        Assert.Contains("\"brightness\"", call.Arguments);
    }

    [Fact]
    public void HasToolCalls_ResponsesOutput_ReturnsTrue()
    {
        Assert.True(_parser.HasToolCalls(ResponsesOutputJson));
    }

    [Fact]
    public void Parse_OutputWithoutFunctionCalls_ReturnsEmpty()
    {
        var json = """
        { "output": [ { "type": "message", "content": [] } ] }
        """;

        Assert.Empty(_parser.Parse(json));
        Assert.False(_parser.HasToolCalls(json));
    }
}
