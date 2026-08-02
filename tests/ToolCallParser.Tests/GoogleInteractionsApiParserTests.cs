using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

/// <summary>
/// Interactions API steps ({"steps":[{"type":"function_call","id":...,"name":...,"arguments":{...}}]}).
/// The Interactions API is generally available and recommended for new projects, and new tool and
/// agentic features launch there; generateContent is the legacy surface. Its tool calls arrive as
/// steps rather than candidates[].content.parts[].functionCall, so they need their own recognition.
/// Format source: ai.google.dev Interactions API reference.
/// </summary>
public class GoogleInteractionsApiParserTests
{
    private readonly GoogleToolCallParser _parser = new();

    // The Interaction resource carries its own top-level "name" — a step must not be confused with it.
    private const string InteractionJson = """
    {
        "name": "interactions/abc123",
        "steps": [
            { "type": "thought", "text": "I should look up the weather." },
            {
                "name": "get_weather",
                "type": "function_call",
                "arguments": { "location": "Boston, MA" },
                "id": "gth23981"
            },
            { "type": "model_output", "content": [{ "text": "checking..." }] }
        ]
    }
    """;

    [Fact]
    public void CanParse_InteractionsSteps_ReturnsTrue()
    {
        using var doc = JsonDocument.Parse(InteractionJson);

        Assert.True(_parser.CanParse(doc.RootElement));
    }

    [Fact]
    public void Parse_InteractionsSteps_ExtractsFunctionCall()
    {
        var result = _parser.Parse(InteractionJson);

        var call = Assert.Single(result);
        Assert.Equal("get_weather", call.Name);

        // The step carries arguments as a JSON object; they must survive as usable JSON.
        var args = call.GetArguments<Dictionary<string, string>>();
        Assert.NotNull(args);
        Assert.Equal("Boston, MA", args["location"]);
    }

    [Fact]
    public void Parse_InteractionsSteps_PreservesStepIdForResultMatching()
    {
        var result = _parser.Parse(InteractionJson);

        // Unlike generateContent (which supplies no id, so one is generated), the Interactions API
        // supplies the id used to submit the matching function_result. Losing it breaks the round trip.
        Assert.Equal("gth23981", Assert.Single(result).Id);
    }

    [Fact]
    public void Parse_InteractionsSteps_IgnoresNonFunctionCallSteps()
    {
        var json = """
        {
            "steps": [
                { "type": "thought", "text": "thinking" },
                { "type": "function_result", "id": "gth1", "result": { "temp": 12 } },
                { "type": "model_output", "content": [{ "text": "done" }] }
            ]
        }
        """;

        Assert.Empty(_parser.Parse(json));
    }

    [Fact]
    public void Parse_InteractionsSteps_ExtractsParallelFunctionCalls()
    {
        var json = """
        {
            "steps": [
                { "name": "get_weather", "type": "function_call", "arguments": { "city": "Boston" }, "id": "a1" },
                { "name": "get_time", "type": "function_call", "arguments": { "tz": "EST" }, "id": "a2" }
            ]
        }
        """;

        var result = _parser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.Equal(["get_weather", "get_time"], result.Select(c => c.Name));
        Assert.Equal(["a1", "a2"], result.Select(c => c.Id));
    }

    [Fact]
    public void HasToolCalls_InteractionsSteps_ReturnsTrue()
    {
        Assert.True(_parser.HasToolCalls(InteractionJson));
    }

    [Fact]
    public void DetectProvider_InteractionsResponse_ReturnsGoogle()
    {
        Assert.Equal(Provider.Google, ToolCallParserFactory.DetectProvider(InteractionJson));
    }

    [Fact]
    public void Parse_InteractionsResponse_ViaFactory_DoesNotSilentlyDropTheCall()
    {
        // Regression: with no parser recognising the steps envelope, auto-detection returned
        // Provider.Auto and the tool call vanished without an error.
        var call = Assert.Single(ToolCallParserFactory.Parse(InteractionJson));

        Assert.Equal("get_weather", call.Name);
    }
}
