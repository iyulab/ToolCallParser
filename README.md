# ToolCallParser

[![NuGet](https://img.shields.io/nuget/v/ToolCallParser.svg)](https://www.nuget.org/packages/ToolCallParser)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

Multi-provider tool call parsing and normalization for LLM applications.

## Packages

| Package | Purpose |
|---------|---------|
| `ToolCallParser` | Parses LLM tool calls from any supported provider's response JSON into one `ToolCall` shape, and formats tool results back to that provider's format |

## Features

No registration or configuration is needed — every entry point is a static method or a public class you construct.

- **Auto-detected parsing** - `ToolCallParserFactory.Parse(string | JsonElement)` detects the format and returns `IReadOnlyList<ToolCall>`; when detection fails it tries each parser in turn. `ToolCallParserFactory.HasToolCalls(...)` answers whether a response contains any calls
- **Provider detection** - `ToolCallParserFactory.DetectProvider(string | JsonElement)` returns the detected `Provider` (`Provider.Auto` when nothing matches)
- **5 provider parsers** - `OpenAIToolCallParser`, `AnthropicToolCallParser`, `GoogleToolCallParser`, `CohereToolCallParser`, `BedrockToolCallParser` (namespace `ToolCallParser.Parsers`), reachable via `ToolCallParserFactory.GetParser(Provider)` / `TryGetParser(Provider, out ...)` or by `new`. They cover OpenAI, Azure OpenAI, xAI, Mistral, DeepSeek, Claude, Gemini, Command R and Bedrock. Two of them read more than one wire format: OpenAI (Chat Completions + Responses) and Google (generateContent + Interactions). `GetParser(Provider.Auto)` throws — use `Parse`/`DetectProvider` for detection
- **Self-hosted / compatible endpoints** - any OpenAI-compatible server (Ollama, vLLM, LM Studio, LocalAI, TGI, GpuStack, Qwen, …) via `Provider.OpenAICompatible`; Anthropic-compatible endpoints via `Provider.AnthropicCompatible`
- **Tool calls written as text** - `ToolCallMarkup.Read(text)` reads the calls an open model wrote in its chat template's markup when the serving endpoint's parser missed them — Hermes / Qwen 2.5 `<tool_call>{json}</tool_call>` and Qwen3-Coder `<function=Name><parameter=x>…` (wrapped or not) — as name + arguments, plus the prose before them and where a block the text ends inside begins; `ToolCallMarkup.Scan(text)` tells a streaming reader whether to hold output (`None` / `Partial` / `Found`)
- **Typed argument access** - `ToolCall.GetArgument<T>(name)`, `GetArguments<T>()` (property names match case-insensitively by default — `ToolCall.DefaultArgumentOptions`), `HasArgument(name)`, `GetArgumentsAsJson()`
- **One answer per input** - detection, `HasToolCalls` and `Parse` agree: text that is not JSON (a plain-text answer) has no tool calls; a call whose arguments are not a JSON object throws `FormatException` naming the tool; a streaming chunk (`choices[].delta`) counts only for calls complete within it — accumulate the stream first
- **Result formatting** - `IToolCallParser.FormatResults(IEnumerable<ToolCallResult>)` with `ToolCallResult.Success(...)` / `Failure(...)`; for Gemini's Interactions surface use `GoogleToolCallParser.FormatResults(results, GoogleSurface.Interactions)`
- **Format categories** - `Provider.GetFormat()`, `IsOpenAICompatible()`, `IsAnthropicCompatible()`, `GetDocumentationUrl()`; `ToolCallParserFactory.GetRegisteredProviders()` / `GetOpenAICompatibleProviders()` list what the factory serves
- **Extensible** - implement `IToolCallParser` (override `CanParse` to control recognition; it defaults to `HasToolCalls`) and call it directly — the factory is a fixed set, not a registry

## Installation

```bash
dotnet add package ToolCallParser
```

## Quick Start

### Auto-Detection Parsing

```csharp
using ToolCallParser;

// Parse tool calls with automatic provider detection
var response = """
{
  "choices": [{
    "message": {
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "get_weather",
          "arguments": "{\"location\":\"Tokyo\",\"unit\":\"celsius\"}"
        }
      }]
    }
  }]
}
""";

var toolCalls = ToolCallParserFactory.Parse(response);

foreach (var call in toolCalls)
{
    Console.WriteLine($"Tool: {call.Name}");
    Console.WriteLine($"ID: {call.Id}");
    Console.WriteLine($"Args: {call.Arguments}");
}
```

### Provider-Specific Parsing

```csharp
// OpenAI-compatible (Mistral, xAI, DeepSeek, or any OpenAI-compatible endpoint)
var openAiParser = ToolCallParserFactory.GetParser(Provider.OpenAI);

// Anthropic Claude
var claudeParser = ToolCallParserFactory.GetParser(Provider.Anthropic);

// Google Gemini
var geminiParser = ToolCallParserFactory.GetParser(Provider.Google);

// AWS Bedrock
var bedrockParser = ToolCallParserFactory.GetParser(Provider.Bedrock);

// Cohere Command R
var cohereParser = ToolCallParserFactory.GetParser(Provider.Cohere);
```

## Supported Providers

### Commercial Cloud

| Provider | Format | Documentation |
|----------|--------|---------------|
| OpenAI | `tool_calls` (Chat Completions) + `function_call` output items (Responses API) | [docs](https://developers.openai.com/api/docs/guides/function-calling) |
| Azure OpenAI | OpenAI-compatible | [docs](https://learn.microsoft.com/azure/ai-services/openai/) |
| Anthropic (Claude) | `tool_use` blocks | [docs](https://docs.anthropic.com/en/docs/build-with-claude/tool-use) |
| Google Gemini | `functionCall` parts (generateContent) + `function_call` steps (Interactions API) | [docs](https://ai.google.dev/gemini-api/docs/function-calling) |
| xAI (Grok) | OpenAI-compatible | [docs](https://docs.x.ai/docs/guides/function-calling) |
| Mistral | OpenAI-compatible | [docs](https://docs.mistral.ai/capabilities/function_calling) |
| Cohere | Unique format | [docs](https://docs.cohere.com/docs/tool-use-overview) |
| DeepSeek | OpenAI-compatible | [docs](https://api-docs.deepseek.com/guides/function_calling) |
| AWS Bedrock | `toolUse` blocks (Converse); also exposes OpenAI-compatible Chat Completions + Responses endpoints, which detect as `Provider.OpenAI` | [docs](https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html) |

### Open Source / Self-Hosted

Self-hosted runtimes speak the OpenAI tool-calling wire format, so they are handled by a
single `Provider.OpenAICompatible` value rather than a per-runtime enum member:

```csharp
// Ollama, vLLM, LM Studio, LocalAI, TGI, GpuStack, Qwen, and any other OpenAI-compatible server
var parser = ToolCallParserFactory.GetParser(Provider.OpenAICompatible);
```

Auto-detection (`ToolCallParserFactory.Parse`) also classifies these responses as the OpenAI
format, so an explicit provider is only needed when you skip detection.

### Format Categories

```csharp
// Check format compatibility
var provider = Provider.OpenAICompatible;

if (provider.IsOpenAICompatible())
{
    Console.WriteLine("Uses OpenAI tool_calls format");
}

if (provider.IsAnthropicCompatible())
{
    Console.WriteLine("Uses Anthropic tool_use format");
}

// Get documentation URL
var docUrl = provider.GetDocumentationUrl();
```

## Response Format Examples

### OpenAI Format (Most Common)

```json
{
  "choices": [{
    "message": {
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "get_weather",
          "arguments": "{\"location\":\"Tokyo\"}"
        }
      }]
    }
  }]
}
```

### Anthropic Format

```json
{
  "content": [
    {"type": "text", "text": "I'll check the weather."},
    {
      "type": "tool_use",
      "id": "toolu_01XYZ",
      "name": "get_weather",
      "input": {"location": "Paris"}
    }
  ],
  "stop_reason": "tool_use"
}
```

### Google Gemini Format

```json
{
  "candidates": [{
    "content": {
      "parts": [{
        "functionCall": {
          "name": "get_weather",
          "args": {"location": "London"}
        }
      }]
    }
  }]
}
```

### AWS Bedrock Format

```json
{
  "output": {
    "message": {
      "content": [{
        "toolUse": {
          "toolUseId": "tooluse_abc",
          "name": "get_weather",
          "input": {"location": "NYC"}
        }
      }]
    }
  },
  "stopReason": "tool_use"
}
```

## Tool Calls Written as Text

```csharp
var text = """
    Saving it.
    <tool_call>
    {"name": "write_file", "arguments": {"path": "a.txt"}}
    </tool_call>
    """;
var read = ToolCallMarkup.Read(text);

Console.WriteLine(read.Prefix); // "Saving it."
foreach (var block in read.Blocks.Where(b => b.IsWellFormed))
{
    // A JSON block gives Arguments (a JSON object); a Qwen3-Coder function block gives Parameters as written.
    Console.WriteLine($"{block.Name}: {block.Arguments ?? string.Join(", ", block.Parameters.Select(p => p.Key))}");
}
```

Whether a block is a call to run — the tool is one you offered, calls are allowed — is your decision, as is typing a
function block's parameter text against the tool's schema.

## Working with Arguments

```csharp
var toolCall = toolCalls[0];

// Get a specific argument
var location = toolCall.GetArgument<string>("location");

// Get all arguments as a typed object
var weather = toolCall.GetArguments<WeatherArgs>();

// Check if argument exists
if (toolCall.HasArgument("unit"))
{
    var unit = toolCall.GetArgument<string>("unit");
}

public record WeatherArgs(string Location, string Unit = "celsius");
```

## Formatting Results

```csharp
// Create tool results
var results = new[]
{
    ToolCallResult.Success("call_abc123", "Temperature: 22°C", "get_weather"),
    ToolCallResult.Failure("call_def456", "Location not found", "get_weather")
};

// Format for OpenAI
var openAiParser = ToolCallParserFactory.GetParser(Provider.OpenAI);
var openAiResults = openAiParser.FormatResults(results);

// Format for Anthropic
var anthropicParser = ToolCallParserFactory.GetParser(Provider.Anthropic);
var claudeResults = anthropicParser.FormatResults(results);

// Format for Google Gemini (generateContent)
var geminiParser = ToolCallParserFactory.GetParser(Provider.Google);
var geminiResults = geminiParser.FormatResults(results);
```

Google serves tool calling through two wire formats, and a result payload carries nothing that says
which one it is headed for — so for the Interactions surface the caller names it. The `call_id` sent
back is the step `id` that parsing preserved:

```csharp
using ToolCallParser.Parsers;

var parser = new GoogleToolCallParser();
var interactionsResults = parser.FormatResults(results, GoogleSurface.Interactions);
// [{ "type": "function_result", "call_id": "...", "name": "...", "result": {...}, "is_error": false }]
```

## Provider Detection

```csharp
// Auto-detect provider from response
var provider = ToolCallParserFactory.DetectProvider(jsonResponse);

switch (provider)
{
    case Provider.OpenAI:
        Console.WriteLine("OpenAI format detected");
        break;
    case Provider.Anthropic:
        Console.WriteLine("Anthropic format detected");
        break;
    case Provider.Google:
        Console.WriteLine("Gemini format detected");
        break;
    case Provider.Bedrock:
        Console.WriteLine("AWS Bedrock format detected");
        break;
    case Provider.Cohere:
        Console.WriteLine("Cohere format detected");
        break;
    case Provider.Auto:
        Console.WriteLine("Could not auto-detect");
        break;
}
```

## Custom Parsers

Implement `IToolCallParser` and use it directly. The built-in `ToolCallParserFactory` exposes a
fixed set of parsers and is not a mutable registry — a custom parser is just a class you own:

```csharp
public class CustomProviderParser : IToolCallParser
{
    public Provider Provider => Provider.OpenAICompatible;
    public IReadOnlyList<ToolCall> Parse(string response) => throw new NotImplementedException(); // your parsing
    public IReadOnlyList<ToolCall> Parse(JsonElement element) => throw new NotImplementedException(); // your parsing
    public bool HasToolCalls(string response) => throw new NotImplementedException(); // your parsing
    public bool HasToolCalls(JsonElement element) => throw new NotImplementedException(); // your parsing
    // Optional: override CanParse for auto-detection; defaults to HasToolCalls.
    public bool CanParse(JsonElement element) => throw new NotImplementedException(); // your parsing
    public string FormatResults(IEnumerable<ToolCallResult> results) => throw new NotImplementedException(); // your parsing
}

// Use it directly
var parser = new CustomProviderParser();
var toolCalls = parser.Parse(response);
```

## API Reference

### ToolCall

```csharp
public sealed record ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }  // JSON string

    public JsonDocument GetArgumentsAsJson();
    public T? GetArguments<T>(JsonSerializerOptions? options = null);
    public T? GetArgument<T>(string name, JsonSerializerOptions? options = null);
    public bool HasArgument(string name);
}
```

### ToolCallResult

```csharp
public sealed record ToolCallResult
{
    public required string ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public required string Content { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? ErrorMessage { get; init; }

    public static ToolCallResult Success(string toolCallId, string content, string? toolName = null);
    public static ToolCallResult Failure(string toolCallId, string errorMessage, string? toolName = null);
}
```

### ProviderExtensions

```csharp
public static class ProviderExtensions
{
    public static ProviderFormat GetFormat(this Provider provider);
    public static bool IsOpenAICompatible(this Provider provider);
    public static bool IsAnthropicCompatible(this Provider provider);
    public static string? GetDocumentationUrl(this Provider provider);
}
```

## Requirements

- .NET 10.0
- System.Text.Json (included in .NET)

## Documentation

- [Tool Call Format Guide](docs/tool-call-format-guide.md) - Detailed format specifications for each provider

## Related Projects

- [TokenMeter](https://github.com/iyulab/TokenMeter) - Model catalog and cost calculation

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

Contributions welcome! Especially for adding support for new providers.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/new-provider`)
3. Add parser in `src/ToolCallParser/Parsers/`
4. Add tests in `tests/ToolCallParser.Tests/`
5. Update Provider enum and factory
6. Submit a Pull Request

See [docs/tool-call-format-guide.md](docs/tool-call-format-guide.md) for detailed instructions.

---

Made with care by [iyulab](https://github.com/iyulab)
