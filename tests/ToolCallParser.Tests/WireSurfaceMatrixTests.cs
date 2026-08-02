namespace ToolCallParser.Tests;

/// <summary>
/// One row per wire surface the library claims to read, driven through the factory rather than
/// through a hand-picked parser.
///
/// The parser-level suites answer "can this parser read this shape?", which is a different question
/// from "does a response arriving from the network reach that parser at all?". A surface added to a
/// parser without a matching detection row is readable in theory and lost in practice — the failure
/// mode behind both the 0.3.1 Cohere V1 fix and the 0.6.0 Responses fix. Providers with more than
/// one surface are where the two questions come apart, so every surface gets its own row.
/// </summary>
public class WireSurfaceMatrixTests
{
    public static TheoryData<string, Provider, string, string, string> Surfaces() => new()
    {
        {
            "openai/chat-completions",
            Provider.OpenAI, "get_weather", "call_abc123",
            """
            {
                "choices": [{
                    "message": {
                        "role": "assistant",
                        "tool_calls": [{
                            "id": "call_abc123",
                            "type": "function",
                            "function": { "name": "get_weather", "arguments": "{\"location\":\"Tokyo\"}" }
                        }]
                    },
                    "finish_reason": "tool_calls"
                }]
            }
            """
        },
        {
            "openai/chat-completions-streaming-delta",
            Provider.OpenAI, "get_weather", "call_stream1",
            """
            {
                "choices": [{
                    "delta": {
                        "tool_calls": [{
                            "id": "call_stream1",
                            "type": "function",
                            "function": { "name": "get_weather", "arguments": "{\"location\":\"Tokyo\"}" }
                        }]
                    }
                }]
            }
            """
        },
        {
            "openai/responses",
            Provider.OpenAI, "get_weather", "call_resp1",
            """
            {
                "id": "resp_1",
                "output": [{
                    "id": "fc_1",
                    "call_id": "call_resp1",
                    "type": "function_call",
                    "name": "get_weather",
                    "arguments": "{\"location\":\"Tokyo\"}"
                }]
            }
            """
        },
        {
            "anthropic/messages",
            Provider.Anthropic, "get_weather", "toolu_01XYZ",
            """
            {
                "content": [
                    { "type": "text", "text": "Checking." },
                    { "type": "tool_use", "id": "toolu_01XYZ", "name": "get_weather", "input": { "location": "Paris" } }
                ],
                "stop_reason": "tool_use"
            }
            """
        },
        {
            "google/generate-content",
            Provider.Google, "get_weather", "",
            """
            {
                "candidates": [{
                    "content": {
                        "parts": [{ "functionCall": { "name": "get_weather", "args": { "location": "London" } } }]
                    },
                    "finishReason": "STOP"
                }]
            }
            """
        },
        {
            "google/interactions",
            Provider.Google, "get_weather", "gth23981",
            """
            {
                "name": "interactions/abc123",
                "steps": [
                    { "type": "thought", "text": "checking weather" },
                    { "type": "function_call", "id": "gth23981", "name": "get_weather", "arguments": { "location": "London" } }
                ]
            }
            """
        },
        {
            "bedrock/converse",
            Provider.Bedrock, "get_weather", "tooluse_abc",
            """
            {
                "output": {
                    "message": {
                        "role": "assistant",
                        "content": [{
                            "toolUse": { "toolUseId": "tooluse_abc", "name": "get_weather", "input": { "location": "NYC" } }
                        }]
                    }
                },
                "stopReason": "tool_use"
            }
            """
        },
        {
            "cohere/v2-chat",
            Provider.Cohere, "get_weather", "call_v2",
            """
            {
                "finish_reason": "TOOL_CALL",
                "message": {
                    "tool_calls": [{
                        "id": "call_v2",
                        "type": "function",
                        "function": { "name": "get_weather", "arguments": "{\"location\":\"Paris\"}" }
                    }]
                }
            }
            """
        },
        {
            "cohere/v2-tool-plan",
            Provider.Cohere, "get_weather", "call_plan",
            """
            {
                "tool_plan": "I will look up the weather.",
                "message": {
                    "tool_calls": [{
                        "id": "call_plan",
                        "type": "function",
                        "function": { "name": "get_weather", "arguments": "{\"location\":\"Paris\"}" }
                    }]
                }
            }
            """
        },
        {
            // Legacy v1 chat: bare {name, parameters} under message, no v2 discriminators present.
            "cohere/v1-chat",
            Provider.Cohere, "get_weather", "",
            """
            {
                "message": {
                    "tool_calls": [{ "name": "get_weather", "parameters": { "location": "Paris" } }]
                }
            }
            """
        },
        {
            "cohere/v1-actions",
            Provider.Cohere, "get_weather", "",
            """
            {
                "actions": [{ "tool_name": "get_weather", "tool_input": { "location": "Paris" } }]
            }
            """
        }
    };

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void Surface_IsAttributed_Parsed_AndKeepsItsCallId(
        string surface, Provider expectedProvider, string expectedName, string expectedId, string json)
    {
        Assert.True(
            ToolCallParserFactory.HasToolCalls(json),
            $"{surface}: tool calls must be visible through the factory");

        Assert.Equal(expectedProvider, ToolCallParserFactory.DetectProvider(json));

        var call = Assert.Single(ToolCallParserFactory.Parse(json));
        Assert.Equal(expectedName, call.Name);
        Assert.False(string.IsNullOrWhiteSpace(call.Id), $"{surface}: call id must never be blank");

        // A blank expectation means the surface carries no id of its own and one is generated.
        if (expectedId.Length > 0)
        {
            Assert.Equal(expectedId, call.Id);
        }
    }
}
