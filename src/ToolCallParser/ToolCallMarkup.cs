using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToolCallParser;

/// <summary>
/// Reads tool calls an open model wrote as text in its chat template's markup, for when the serving endpoint's parser did
/// not turn them into structured calls.
/// </summary>
/// <remarks>
/// <para>
/// Two forms are read: a JSON block, <c>&lt;tool_call&gt;{"name": …, "arguments": …}&lt;/tool_call&gt;</c> (Hermes, Qwen
/// 2.5), and a function block,
/// <c>&lt;tool_call&gt;&lt;function=Name&gt;&lt;parameter=x&gt;…&lt;/parameter&gt;&lt;/function&gt;&lt;/tool_call&gt;</c>
/// (Qwen3-Coder), with or without the <c>&lt;tool_call&gt;</c> wrapper.
/// </para>
/// <para>
/// This type reads markup only. Whether a block is a call the caller should run — the tool is one it offered, calls are
/// allowed — is the caller's decision, as is typing a function block's parameter text against the tool's schema.
/// </para>
/// </remarks>
public static partial class ToolCallMarkup
{
    private const string WrapperMarker = "<tool_call>";
    private const string FunctionMarker = "<function=";
    private static readonly string[] Markers = [WrapperMarker, FunctionMarker];

    /// <summary>
    /// Whether <paramref name="text"/> holds a block marker (<see cref="ToolCallMarkupScan.Found"/>), ends with what may
    /// become one (<see cref="ToolCallMarkupScan.Partial"/> — a streamed <c>&lt;tool_</c> whose next chunk decides), or
    /// neither. A streaming reader holds output back while the answer is not <see cref="ToolCallMarkupScan.None"/>.
    /// </summary>
    public static ToolCallMarkupScan Scan(ReadOnlySpan<char> text)
    {
        foreach (var marker in Markers)
        {
            if (text.IndexOf(marker.AsSpan(), StringComparison.Ordinal) >= 0)
            {
                return ToolCallMarkupScan.Found;
            }
        }

        var lastOpen = text.LastIndexOf('<');
        if (lastOpen < 0)
        {
            return ToolCallMarkupScan.None;
        }

        var tail = text[lastOpen..];
        foreach (var marker in Markers)
        {
            if (marker.AsSpan().StartsWith(tail, StringComparison.Ordinal))
            {
                return ToolCallMarkupScan.Partial;
            }
        }

        return ToolCallMarkupScan.None;
    }

    /// <summary>Reads the call blocks in <paramref name="text"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    public static ToolCallMarkupText Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<(int Start, string Body)>();
        foreach (Match m in ToolCallBlock().Matches(text))
        {
            found.Add((m.Index, m.Groups["body"].Value));
        }

        if (found.Count == 0)
        {
            // Qwen3-Coder sometimes leaves out the <tool_call> wrapper.
            foreach (Match m in FunctionBlock().Matches(text))
            {
                found.Add((m.Index, m.Value));
            }
        }

        if (found.Count == 0)
        {
            return new ToolCallMarkupText
            {
                Prefix = text,
                Blocks = [],
                UnterminatedAt = UnterminatedBlockStart(text),
            };
        }

        var blocks = new List<ToolCallMarkupBlock>(found.Count);
        foreach (var (start, body) in found)
        {
            blocks.Add(ReadBlock(start, body.Trim()));
        }

        // A wrapper opened but never closed («<tool_call> <function=…>…</function>») is part of the call, not of the prose.
        var prefix = text[..found[0].Start].TrimEnd();
        if (prefix.EndsWith(WrapperMarker, StringComparison.Ordinal))
        {
            prefix = prefix[..^WrapperMarker.Length].TrimEnd();
        }

        return new ToolCallMarkupText { Prefix = prefix, Blocks = blocks, UnterminatedAt = null };
    }

    private static ToolCallMarkupBlock ReadBlock(int start, string body)
    {
        if (body.StartsWith('{'))
        {
            return ReadJsonBlock(start, body);
        }

        var function = FunctionBlock().Match(body);
        if (!function.Success)
        {
            return new ToolCallMarkupBlock { Start = start, Format = ToolCallMarkupFormat.Function };
        }

        var parameters = new List<KeyValuePair<string, string>>();
        foreach (Match parameter in ParameterBlock().Matches(function.Groups["params"].Value))
        {
            parameters.Add(new(parameter.Groups["name"].Value.Trim(), TrimOneNewline(parameter.Groups["value"].Value)));
        }

        return new ToolCallMarkupBlock
        {
            Start = start,
            Format = ToolCallMarkupFormat.Function,
            Name = function.Groups["name"].Value.Trim(),
            Parameters = parameters,
        };
    }

    private static ToolCallMarkupBlock ReadJsonBlock(int start, string body)
    {
        var malformed = new ToolCallMarkupBlock { Start = start, Format = ToolCallMarkupFormat.Json };
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            {
                return malformed;
            }

            var arguments = "{}";
            if (root.TryGetProperty("arguments", out var args) || root.TryGetProperty("parameters", out args))
            {
                switch (args.ValueKind)
                {
                    case JsonValueKind.Object:
                        arguments = args.GetRawText();
                        break;
                    case JsonValueKind.String:
                        using (var inner = JsonDocument.Parse(args.GetString()!))
                        {
                            if (inner.RootElement.ValueKind != JsonValueKind.Object)
                            {
                                return malformed;
                            }

                            arguments = inner.RootElement.GetRawText();
                        }

                        break;
                    case JsonValueKind.Null:
                        break;
                    default:
                        return malformed;
                }
            }

            return new ToolCallMarkupBlock
            {
                Start = start,
                Format = ToolCallMarkupFormat.Json,
                Name = name.GetString(),
                Arguments = arguments,
            };
        }
        catch (JsonException)
        {
            return malformed;
        }
    }

    /// <summary>Where the block <paramref name="text"/> ends inside begins, or <c>null</c> when it does not end inside one.</summary>
    /// <remarks>
    /// A marker counts only when what follows it is a block's opening — a JSON object, a <c>&lt;function=Name&gt;</c>, a
    /// parameter, or nothing yet — so prose that names the format («the format is &lt;tool_call&gt;, followed by JSON») stays.
    /// </remarks>
    private static int? UnterminatedBlockStart(string text)
    {
        var wrapper = text.IndexOf(WrapperMarker, StringComparison.Ordinal);
        if (wrapper >= 0)
        {
            var rest = text.AsSpan(wrapper + WrapperMarker.Length).TrimStart();
            return rest.IsEmpty || rest[0] == '{' || IsFunctionOpening(rest) ? wrapper : null;
        }

        var function = text.IndexOf(FunctionMarker, StringComparison.Ordinal);
        return function >= 0 && IsFunctionOpening(text.AsSpan(function)) ? function : null;
    }

    /// <summary>Whether <paramref name="tail"/> is the start of a <c>&lt;function=Name&gt;</c> block cut off before its end.</summary>
    private static bool IsFunctionOpening(ReadOnlySpan<char> tail)
    {
        var marker = FunctionMarker.AsSpan();
        if (tail.Length <= marker.Length)
        {
            return marker.StartsWith(tail, StringComparison.Ordinal);
        }

        if (!tail.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var afterMarker = tail[marker.Length..];
        var close = afterMarker.IndexOf('>');
        if (close < 0)
        {
            return afterMarker.IndexOfAny(" \t\r\n") < 0; // a name still being written
        }

        var body = afterMarker[(close + 1)..].TrimStart();
        return body.IsEmpty || body[0] == '<';
    }

    private static string TrimOneNewline(string value)
    {
        if (value.StartsWith("\r\n", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (value.StartsWith('\n'))
        {
            value = value[1..];
        }

        if (value.EndsWith("\r\n", StringComparison.Ordinal))
        {
            value = value[..^2];
        }
        else if (value.EndsWith('\n'))
        {
            value = value[..^1];
        }

        return value;
    }

    [GeneratedRegex(@"<tool_call>(?<body>.*?)</tool_call>", RegexOptions.Singleline)]
    private static partial Regex ToolCallBlock();

    [GeneratedRegex(@"<function=(?<name>[^>\n]+)>(?<params>.*?)</function>", RegexOptions.Singleline)]
    private static partial Regex FunctionBlock();

    [GeneratedRegex(@"<parameter=(?<name>[^>\n]+)>(?<value>.*?)</parameter>", RegexOptions.Singleline)]
    private static partial Regex ParameterBlock();
}

/// <summary>What <see cref="ToolCallMarkup.Scan"/> found at the end of a text read so far.</summary>
public enum ToolCallMarkupScan
{
    /// <summary>No block marker and nothing that may become one.</summary>
    None,

    /// <summary>The text ends with a proper prefix of a block marker; the next chunk decides.</summary>
    Partial,

    /// <summary>The text holds a block marker.</summary>
    Found,
}

/// <summary>The markup form a block was written in.</summary>
public enum ToolCallMarkupFormat
{
    /// <summary><c>&lt;tool_call&gt;{"name": …, "arguments": …}&lt;/tool_call&gt;</c>.</summary>
    Json,

    /// <summary><c>&lt;function=Name&gt;&lt;parameter=x&gt;…&lt;/parameter&gt;&lt;/function&gt;</c>, wrapped or not.</summary>
    Function,
}

/// <summary>A text read by <see cref="ToolCallMarkup.Read"/>.</summary>
public sealed record ToolCallMarkupText
{
    /// <summary>
    /// The text before the first complete block, trailing whitespace (and an unclosed <c>&lt;tool_call&gt;</c> wrapper)
    /// removed — or the whole text when it holds no complete block.
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>The complete blocks, in order. Text after the last block is not kept.</summary>
    public required IReadOnlyList<ToolCallMarkupBlock> Blocks { get; init; }

    /// <summary>
    /// Where a block the text ends inside begins — the server stopped mid-call — when the text holds no complete block;
    /// otherwise <c>null</c>.
    /// </summary>
    public int? UnterminatedAt { get; init; }
}

/// <summary>One complete call block.</summary>
public sealed record ToolCallMarkupBlock
{
    /// <summary>The block's position in the text read.</summary>
    public required int Start { get; init; }

    /// <summary>The form the block was written in.</summary>
    public required ToolCallMarkupFormat Format { get; init; }

    /// <summary>The tool name, or <c>null</c> when the block is not well formed (no name, JSON that does not parse,
    /// arguments that are not an object).</summary>
    public string? Name { get; init; }

    /// <summary>A <see cref="ToolCallMarkupFormat.Json"/> block's arguments as a JSON object (<c>{}</c> when it gives none);
    /// <c>null</c> for a function block.</summary>
    public string? Arguments { get; init; }

    /// <summary>A <see cref="ToolCallMarkupFormat.Function"/> block's parameters as written (one leading and one trailing
    /// newline removed), in order; empty for a JSON block.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Parameters { get; init; } = [];

    /// <summary>Whether the block names a tool and its arguments read.</summary>
    public bool IsWellFormed => Name is not null;
}
