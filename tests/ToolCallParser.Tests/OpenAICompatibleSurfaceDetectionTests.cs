using System.Text.Json;

namespace ToolCallParser.Tests;

/// <summary>
/// Auto-detection over OpenAI-compatible surfaces reached through a non-OpenAI vendor.
/// Several providers expose the OpenAI Chat Completions and Responses wire formats verbatim and
/// document only a base-URL change, so the same payloads arrive from more than one origin.
/// Detection must therefore survive every probe ahead of OpenAI in the order, not just the
/// OpenAI parser itself — the parser-level tests never exercise that path.
/// </summary>
public class OpenAICompatibleSurfaceDetectionTests
{
    /// <summary>
    /// Responses API payload. The root <c>output</c> is an ARRAY here, whereas the Converse-style
    /// format uses an <c>output</c> OBJECT — the collision this fixture pins down.
    /// </summary>
    private const string ResponsesApiJson = """
    {
        "id": "resp_abc123",
        "object": "response",
        "model": "openai.gpt-oss-120b",
        "output": [
            {
                "id": "fc_9001",
                "call_id": "call_9001",
                "type": "function_call",
                "name": "get_weather",
                "arguments": "{\"location\":\"Seattle, WA\"}"
            }
        ]
    }
    """;

    private const string ChatCompletionsJson = """
    {
        "id": "chatcmpl-abc123",
        "object": "chat.completion",
        "model": "openai.gpt-oss-120b",
        "choices": [{
            "message": {
                "role": "assistant",
                "tool_calls": [{
                    "id": "call_7001",
                    "type": "function",
                    "function": {
                        "name": "get_weather",
                        "arguments": "{\"location\":\"Seattle, WA\"}"
                    }
                }]
            },
            "finish_reason": "tool_calls"
        }]
    }
    """;

    /// <summary>Converse API — the native format, which must keep its own attribution.</summary>
    private const string ConverseJson = """
    {
        "output": {
            "message": {
                "role": "assistant",
                "content": [{
                    "toolUse": {
                        "toolUseId": "tooluse_5001",
                        "name": "get_weather",
                        "input": { "location": "Seattle, WA" }
                    }
                }]
            }
        },
        "stopReason": "tool_use"
    }
    """;

    [Fact]
    public void DetectProvider_ResponsesApiPayload_DoesNotThrow()
    {
        var exception = Record.Exception(() => ToolCallParserFactory.DetectProvider(ResponsesApiJson));

        Assert.Null(exception);
    }

    [Fact]
    public void DetectProvider_ResponsesApiPayload_ReturnsOpenAI()
    {
        Assert.Equal(Provider.OpenAI, ToolCallParserFactory.DetectProvider(ResponsesApiJson));
    }

    [Fact]
    public void Parse_ResponsesApiPayload_ViaFactory_ReturnsTheCall()
    {
        var result = ToolCallParserFactory.Parse(ResponsesApiJson);

        var call = Assert.Single(result);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("call_9001", call.Id);
    }

    [Fact]
    public void HasToolCalls_ResponsesApiPayload_ViaFactory_ReturnsTrue()
    {
        Assert.True(ToolCallParserFactory.HasToolCalls(ResponsesApiJson));
    }

    [Fact]
    public void DetectProvider_ChatCompletionsPayload_ReturnsOpenAI()
    {
        Assert.Equal(Provider.OpenAI, ToolCallParserFactory.DetectProvider(ChatCompletionsJson));
    }

    [Fact]
    public void Parse_ChatCompletionsPayload_ViaFactory_ReturnsTheCall()
    {
        var result = ToolCallParserFactory.Parse(ChatCompletionsJson);

        var call = Assert.Single(result);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("call_7001", call.Id);
    }

    [Fact]
    public void DetectProvider_ConverseApiPayload_StillReturnsBedrock()
    {
        Assert.Equal(Provider.Bedrock, ToolCallParserFactory.DetectProvider(ConverseJson));
    }

    [Fact]
    public void Parse_ConverseApiPayload_ViaFactory_ReturnsTheCall()
    {
        var result = ToolCallParserFactory.Parse(ConverseJson);

        var call = Assert.Single(result);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal("tooluse_5001", call.Id);
    }

    [Fact]
    public void BedrockParser_ResponsesApiPayload_DoesNotClaimIt()
    {
        using var doc = JsonDocument.Parse(ResponsesApiJson);
        var parser = ToolCallParserFactory.GetParser(Provider.Bedrock);

        Assert.False(parser.CanParse(doc.RootElement));
        Assert.False(parser.HasToolCalls(doc.RootElement));
        Assert.Empty(parser.Parse(doc.RootElement));
    }
}
