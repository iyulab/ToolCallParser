using System.Text.Json;
using ToolCallParser.Parsers;

namespace ToolCallParser.Tests;

/// <summary>
/// Locks the single-source detection design (issue item 3 / decision D3): format
/// recognition lives in each parser's <see cref="IToolCallParser.CanParse"/>, and
/// <see cref="ToolCallParserFactory.DetectProvider(JsonElement)"/> only orders the probes.
/// </summary>
public class CanParseDelegationTests
{
    private static JsonElement El(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void EachParser_RecognizesItsOwnCanonicalFormat()
    {
        Assert.True(new OpenAIToolCallParser().CanParse(El("""{ "choices": [] }""")));
        Assert.True(new AnthropicToolCallParser().CanParse(El("""{ "stop_reason": "tool_use" }""")));
        Assert.True(new GoogleToolCallParser().CanParse(El("""{ "candidates": [ { "content": { "parts": [ { "functionCall": {} } ] } } ] }""")));
        Assert.True(new BedrockToolCallParser().CanParse(El("""{ "stopReason": "tool_use" }""")));
        Assert.True(new CohereToolCallParser().CanParse(El("""{ "finish_reason": "TOOL_CALL" }""")));
    }

    [Fact]
    public void CanParse_RejectsForeignFormat()
    {
        // A pure Anthropic marker must not be claimed by the Cohere/Google/Bedrock parsers.
        var anthropic = El("""{ "stop_reason": "end_turn" }""");
        Assert.False(new GoogleToolCallParser().CanParse(anthropic));
        Assert.False(new BedrockToolCallParser().CanParse(anthropic));
        Assert.False(new CohereToolCallParser().CanParse(anthropic));
    }

    [Fact]
    public void DetectProvider_AgreesWithParserCanParse()
    {
        // The factory's decision is exactly "first parser (in probe order) whose CanParse is true".
        var element = El("""{ "finish_reason": "TOOL_CALL" }""");
        var detected = ToolCallParserFactory.DetectProvider(element);
        Assert.Equal(Provider.Cohere, detected);
        Assert.True(ToolCallParserFactory.GetParser(detected).CanParse(element));
    }

    [Fact]
    public void DefaultInterfaceMethod_FallsBackToHasToolCalls()
    {
        // A custom parser that does not override CanParse inherits the default (HasToolCalls).
        IToolCallParser custom = new HasToolCallsStub(hasToolCalls: true);
        Assert.True(custom.CanParse(El("{}")));

        IToolCallParser none = new HasToolCallsStub(hasToolCalls: false);
        Assert.False(none.CanParse(El("{}")));
    }

    private sealed class HasToolCallsStub(bool hasToolCalls) : IToolCallParser
    {
        public Provider Provider => Provider.OpenAICompatible;
        public IReadOnlyList<ToolCall> Parse(string response) => [];
        public IReadOnlyList<ToolCall> Parse(JsonElement element) => [];
        public bool HasToolCalls(string response) => hasToolCalls;
        public bool HasToolCalls(JsonElement element) => hasToolCalls;
        public string FormatResults(IEnumerable<ToolCallResult> results) => "[]";
    }
}
