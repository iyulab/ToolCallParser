using System.Text.Json;

namespace ToolCallParser.Tests;

/// <summary>
/// Output-correctness coverage for <c>IToolCallParser.FormatResults</c>.
/// The existing suite is Parse-direction heavy; the 2026-07-06 code-quality review
/// flagged the missing FormatResults output assertions. These lock in the wire shape
/// each provider emits for a tool result.
/// </summary>
public class FormatResultsTests
{
    private static JsonElement Format(Provider provider, ToolCallResult result)
    {
        var json = ToolCallParserFactory.GetParser(provider).FormatResults([result]);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void OpenAI_ProducesToolRoleMessage()
    {
        var msg = Format(Provider.OpenAI, ToolCallResult.Success("call_1", "22C", "get_weather"))
            .EnumerateArray().Single();
        Assert.Equal("tool", msg.GetProperty("role").GetString());
        Assert.Equal("call_1", msg.GetProperty("tool_call_id").GetString());
        Assert.Equal("22C", msg.GetProperty("content").GetString());
    }

    [Fact]
    public void Anthropic_ProducesToolResultBlock_WithErrorFlag()
    {
        var root = Format(Provider.Anthropic, ToolCallResult.Failure("toolu_1", "boom", "get_weather"));
        Assert.Equal("user", root.GetProperty("role").GetString());
        var block = root.GetProperty("content").EnumerateArray().Single();
        Assert.Equal("tool_result", block.GetProperty("type").GetString());
        Assert.Equal("toolu_1", block.GetProperty("tool_use_id").GetString());
        Assert.True(block.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public void Google_ProducesFunctionResponse()
    {
        var fr = Format(Provider.Google, ToolCallResult.Success("id", "25C", "get_weather"))
            .EnumerateArray().Single().GetProperty("functionResponse");
        Assert.Equal("get_weather", fr.GetProperty("name").GetString());
        Assert.Equal("25C", fr.GetProperty("response").GetProperty("result").GetString());
    }

    [Fact]
    public void Bedrock_ProducesToolResult_WithStatus()
    {
        var toolResult = Format(Provider.Bedrock, ToolCallResult.Success("tu_1", "ok", "get_weather"))
            .GetProperty("content").EnumerateArray().Single().GetProperty("toolResult");
        Assert.Equal("tu_1", toolResult.GetProperty("toolUseId").GetString());
        Assert.Equal("success", toolResult.GetProperty("status").GetString());
    }

    // Characterization test — documents CURRENT (defective) Cohere behavior:
    // `call.parameters` is hard-coded empty because ToolCallResult carries no original
    // arguments to echo. This is a KNOWN LIMITATION, not the desired output.
    // See modules/ToolCallParser/claudedocs/issues/ISSUE-ToolCallParser-...-cohere-bug.md.
    // Update this assertion when the bug is fixed.
    [Fact]
    public void Cohere_CurrentBehavior_ParametersAreEmpty()
    {
        var call = Format(Provider.Cohere, ToolCallResult.Success("id", "18C", "get_weather"))
            .GetProperty("tool_results").EnumerateArray().Single().GetProperty("call");
        Assert.Equal("get_weather", call.GetProperty("name").GetString());
        Assert.Empty(call.GetProperty("parameters").EnumerateObject());
    }
}
