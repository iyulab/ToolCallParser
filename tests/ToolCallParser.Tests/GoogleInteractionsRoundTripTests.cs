using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

/// <summary>
/// Interactions round trip: parse a call, run the tool, hand the result back.
///
/// The two directions have to agree or neither is usable. Parsing preserves the step <c>id</c>
/// precisely because the submission references it as <c>call_id</c> — a formatter that drops it
/// makes that preservation pointless and breaks the conversation. Reading a format without being
/// able to answer in it is half a feature.
/// </summary>
public class GoogleInteractionsRoundTripTests
{
    private readonly GoogleToolCallParser _parser = new();

    private const string InteractionsResponse = """
    {
        "name": "interactions/abc123",
        "steps": [
            { "type": "thought", "text": "I should check the weather" },
            {
                "type": "function_call",
                "id": "gth23981",
                "name": "get_weather",
                "arguments": { "location": "Boston, MA" }
            }
        ]
    }
    """;

    private static JsonElement FormatAndReadSingle(GoogleToolCallParser parser, ToolCallResult result)
    {
        var json = parser.FormatResults([result], GoogleSurface.Interactions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());

        return doc.RootElement[0].Clone();
    }

    [Fact]
    public void FormatResults_Interactions_CarriesBackTheParsedCallId()
    {
        var call = Assert.Single(_parser.Parse(InteractionsResponse));

        var item = FormatAndReadSingle(_parser, ToolCallResult.Success(call.Id, """{"tempC":12}""", call.Name));

        Assert.Equal("gth23981", item.GetProperty("call_id").GetString());
    }

    [Fact]
    public void FormatResults_Interactions_EmitsFunctionResultType()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Success("gth23981", "ok", "get_weather"));

        Assert.Equal("function_result", item.GetProperty("type").GetString());
    }

    [Fact]
    public void FormatResults_Interactions_CarriesTheToolName()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Success("gth23981", "ok", "get_weather"));

        Assert.Equal("get_weather", item.GetProperty("name").GetString());
    }

    [Fact]
    public void FormatResults_Interactions_SuccessIsNotAnError()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Success("gth23981", "ok", "get_weather"));

        Assert.False(item.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public void FormatResults_Interactions_FailureIsMarkedAsError()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Failure("gth23981", "Location not found", "get_weather"));

        Assert.True(item.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public void FormatResults_Interactions_JsonContentIsEmbeddedNotStringified()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Success("gth23981", """{"tempC":12}""", "get_weather"));

        var result = item.GetProperty("result");
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(12, result.GetProperty("tempC").GetInt32());
    }

    [Fact]
    public void FormatResults_Interactions_PlainTextContentIsWrappedInAnObject()
    {
        var item = FormatAndReadSingle(_parser, ToolCallResult.Success("gth23981", "22 degrees", "get_weather"));

        var result = item.GetProperty("result");
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("22 degrees", result.GetProperty("result").GetString());
    }

    [Fact]
    public void FormatResults_Interactions_FormatsEveryResult()
    {
        var json = _parser.FormatResults(
            [
                ToolCallResult.Success("id_1", "a", "tool_a"),
                ToolCallResult.Success("id_2", "b", "tool_b")
            ],
            GoogleSurface.Interactions);

        using var doc = JsonDocument.Parse(json);

        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("id_1", doc.RootElement[0].GetProperty("call_id").GetString());
        Assert.Equal("id_2", doc.RootElement[1].GetProperty("call_id").GetString());
    }

    [Fact]
    public void FormatResults_GenerateContentSurface_MatchesTheParameterlessOverload()
    {
        ToolCallResult[] results = [ToolCallResult.Success("call_1", "ok", "get_weather")];

        Assert.Equal(
            _parser.FormatResults(results),
            _parser.FormatResults(results, GoogleSurface.GenerateContent));
    }

    [Fact]
    public void FormatResults_GenerateContentSurface_StillEmitsFunctionResponse()
    {
        var json = _parser.FormatResults(
            [ToolCallResult.Success("call_1", "ok", "get_weather")],
            GoogleSurface.GenerateContent);

        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement[0].TryGetProperty("functionResponse", out _));
    }
}
