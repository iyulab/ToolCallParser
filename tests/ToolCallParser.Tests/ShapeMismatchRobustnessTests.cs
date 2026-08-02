using System.Text.Json;

namespace ToolCallParser.Tests;

/// <summary>
/// Detection runs against payloads whose shape is not yet known — that is what detection is for.
/// A probe must therefore answer "not mine" for any shape it does not recognise, never fault.
/// Each case below places a value of an unexpected kind where a parser walks a property chain
/// (an array where an object is expected, a scalar inside a block list, a non-object root).
/// These are not exotic: a provider that renames a field, an error envelope, or a second vendor
/// reusing a container name all produce them.
/// </summary>
public class ShapeMismatchRobustnessTests
{
    public static TheoryData<string, string> MismatchedShapes() => new()
    {
        // Root is an array — every parser starts by reading a property off the root.
        { "array root", """[{ "name": "get_weather" }]""" },
        { "string root", "\"just text\"" },
        { "number root", "42" },

        // Converse-style walk: output -> message -> content
        { "output as array", """{ "output": [{ "type": "function_call", "name": "f" }] }""" },
        { "output.message as array", """{ "output": { "message": ["x"] } }""" },
        { "message as scalar", """{ "message": "hello" }""" },

        // Gemini-style walk: candidates[] -> content -> parts
        { "candidate as scalar", """{ "candidates": ["text"] }""" },
        { "candidate.content as string", """{ "candidates": [{ "content": "text" }] }""" },
        { "content.parts as object", """{ "content": { "parts": { "functionCall": {} } } }""" },

        // Chat Completions walk: choices[] -> message -> tool_calls
        { "choice as scalar", """{ "choices": ["text"] }""" },
        { "choice.message as string", """{ "choices": [{ "message": "hi" }] }""" },
        { "choice.delta as string", """{ "choices": [{ "delta": "hi" }] }""" },

        // Block lists whose members are not objects
        { "anthropic content block as scalar", """{ "content": ["text"], "stop_reason": "end_turn" }""" },
        { "cohere action as scalar", """{ "actions": ["text"] }""" },
        { "cohere tool_call as scalar", """{ "tool_calls": ["text"] }""" },

        // Responses-style output items that are not objects
        { "output item as scalar", """{ "output": ["text"] }""" }
    };

    [Theory]
    [MemberData(nameof(MismatchedShapes))]
    public void DetectProvider_MismatchedShape_DoesNotThrow(string label, string json)
    {
        var exception = Record.Exception(() => ToolCallParserFactory.DetectProvider(json));

        Assert.True(exception is null, $"{label}: DetectProvider threw {exception?.GetType().Name}: {exception?.Message}");
    }

    [Theory]
    [MemberData(nameof(MismatchedShapes))]
    public void Parse_MismatchedShape_DoesNotThrow(string label, string json)
    {
        var exception = Record.Exception(() => ToolCallParserFactory.Parse(json));

        Assert.True(exception is null, $"{label}: Parse threw {exception?.GetType().Name}: {exception?.Message}");
    }

    [Theory]
    [MemberData(nameof(MismatchedShapes))]
    public void HasToolCalls_MismatchedShape_DoesNotThrow(string label, string json)
    {
        var exception = Record.Exception(() => ToolCallParserFactory.HasToolCalls(json));

        Assert.True(exception is null, $"{label}: HasToolCalls threw {exception?.GetType().Name}: {exception?.Message}");
    }

    [Theory]
    [MemberData(nameof(MismatchedShapes))]
    public void EveryParser_MismatchedShape_DoesNotThrow(string label, string json)
    {
        using var doc = JsonDocument.Parse(json);

        foreach (var provider in ToolCallParserFactory.GetRegisteredProviders())
        {
            var parser = ToolCallParserFactory.GetParser(provider);

            var canParse = Record.Exception(() => parser.CanParse(doc.RootElement));
            Assert.True(canParse is null, $"{label}: {provider}.CanParse threw {canParse?.GetType().Name}");

            var parse = Record.Exception(() => parser.Parse(doc.RootElement));
            Assert.True(parse is null, $"{label}: {provider}.Parse threw {parse?.GetType().Name}");

            var hasToolCalls = Record.Exception(() => parser.HasToolCalls(doc.RootElement));
            Assert.True(hasToolCalls is null, $"{label}: {provider}.HasToolCalls threw {hasToolCalls?.GetType().Name}");
        }
    }
}
