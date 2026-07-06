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

    // Cohere Chat API v2: tool results are messages with role "tool", tool_call_id
    // (for matching, no parameter echo), and content. Fixes the prior v1/v2 mismatch
    // where FormatResults emitted the legacy v1 `tool_results[].call.parameters={}`
    // shape and discarded the tool_call_id the parser had extracted.
    [Fact]
    public void Cohere_ProducesToolRoleMessage_WithToolCallId()
    {
        var msg = Format(Provider.Cohere, ToolCallResult.Success("call_1", "18C", "get_weather"))
            .EnumerateArray().Single();
        Assert.Equal("tool", msg.GetProperty("role").GetString());
        Assert.Equal("call_1", msg.GetProperty("tool_call_id").GetString());
        Assert.Equal("18C", msg.GetProperty("content").GetString());
    }

    [Fact]
    public void Cohere_FailureResult_CarriesErrorTextInContent()
    {
        var msg = Format(Provider.Cohere, ToolCallResult.Failure("call_9", "boom", "get_weather"))
            .EnumerateArray().Single();
        Assert.Equal("call_9", msg.GetProperty("tool_call_id").GetString());
        Assert.Contains("boom", msg.GetProperty("content").GetString());
    }
}
