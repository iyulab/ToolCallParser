using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

/// <summary>
/// Cross-provider round trip: parse a call, format a result for it, and check the vendor's call id
/// survives the trip.
///
/// A tool call is only useful if the answer can be matched back to it. Every provider here except
/// one carries an id in its result format for exactly that purpose, so a formatter that drops or
/// regenerates it produces output the vendor cannot match — silently, since the payload still looks
/// well-formed. This has already happened twice (0.3.0 D1 on Cohere, 0.6.0 on Google Interactions),
/// both times because a surface was added to the reading side while the writing side stayed behind.
/// One row per provider is what keeps the two sides moving together.
/// </summary>
public class RoundTripCallIdTests
{
    private sealed record RoundTrip(
        string Response,
        string ExpectedId,
        Func<IToolCallParser, IEnumerable<ToolCallResult>, string> Format,
        Func<JsonElement, string?> ReadId);

    private static readonly Dictionary<string, RoundTrip> Cases = new()
    {
        ["openai"] = new(
            """
            {
                "choices": [{
                    "message": {
                        "tool_calls": [{
                            "id": "call_abc123",
                            "type": "function",
                            "function": { "name": "get_weather", "arguments": "{}" }
                        }]
                    }
                }]
            }
            """,
            "call_abc123",
            (parser, results) => parser.FormatResults(results),
            root => root[0].GetProperty("tool_call_id").GetString()),

        ["anthropic"] = new(
            """
            {
                "content": [{ "type": "tool_use", "id": "toolu_01XYZ", "name": "get_weather", "input": {} }],
                "stop_reason": "tool_use"
            }
            """,
            "toolu_01XYZ",
            (parser, results) => parser.FormatResults(results),
            root => root.GetProperty("content")[0].GetProperty("tool_use_id").GetString()),

        ["bedrock"] = new(
            """
            {
                "output": {
                    "message": {
                        "content": [{
                            "toolUse": { "toolUseId": "tooluse_abc", "name": "get_weather", "input": {} }
                        }]
                    }
                },
                "stopReason": "tool_use"
            }
            """,
            "tooluse_abc",
            (parser, results) => parser.FormatResults(results),
            root => root.GetProperty("content")[0].GetProperty("toolResult").GetProperty("toolUseId").GetString()),

        ["cohere-v2"] = new(
            """
            {
                "finish_reason": "TOOL_CALL",
                "message": {
                    "tool_calls": [{
                        "id": "call_v2",
                        "type": "function",
                        "function": { "name": "get_weather", "arguments": "{}" }
                    }]
                }
            }
            """,
            "call_v2",
            (parser, results) => parser.FormatResults(results),
            root => root[0].GetProperty("tool_call_id").GetString()),

        ["google-interactions"] = new(
            """
            {
                "name": "interactions/abc123",
                "steps": [{
                    "type": "function_call",
                    "id": "gth23981",
                    "name": "get_weather",
                    "arguments": {}
                }]
            }
            """,
            "gth23981",
            (parser, results) => ((GoogleToolCallParser)parser).FormatResults(results, GoogleSurface.Interactions),
            root => root[0].GetProperty("call_id").GetString())
    };

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var name in Cases.Keys)
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void RoundTrip_PreservesVendorCallId(string caseName)
    {
        var testCase = Cases[caseName];

        var provider = ToolCallParserFactory.DetectProvider(testCase.Response);
        var parser = ToolCallParserFactory.GetParser(provider);

        var call = Assert.Single(parser.Parse(testCase.Response));
        Assert.Equal(testCase.ExpectedId, call.Id);

        // Feed the parsed id straight back — a hard-coded id here would pass even if parsing lost it.
        var formatted = testCase.Format(parser, [ToolCallResult.Success(call.Id, "ok", call.Name)]);

        using var doc = JsonDocument.Parse(formatted);
        Assert.Equal(testCase.ExpectedId, testCase.ReadId(doc.RootElement));
    }

    /// <summary>
    /// generateContent is the documented exception: its <c>functionResponse</c> carries a name and a
    /// response body but no call id, because the surface matches results to calls by name and order
    /// rather than by id. Pinned so the absence stays a known vendor property instead of looking
    /// like the defect this suite exists to catch.
    /// </summary>
    [Fact]
    public void RoundTrip_GoogleGenerateContent_HasNoCallIdByDesign()
    {
        var parser = new GoogleToolCallParser();

        var formatted = parser.FormatResults([ToolCallResult.Success("whatever", "ok", "get_weather")]);

        using var doc = JsonDocument.Parse(formatted);
        var functionResponse = doc.RootElement[0].GetProperty("functionResponse");

        Assert.Equal("get_weather", functionResponse.GetProperty("name").GetString());
        Assert.False(functionResponse.TryGetProperty("call_id", out _));
        Assert.False(functionResponse.TryGetProperty("id", out _));
    }
}
