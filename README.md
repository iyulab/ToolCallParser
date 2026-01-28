# ToolCallParser

[![NuGet](https://img.shields.io/nuget/v/ToolCallParser.svg)](https://www.nuget.org/packages/ToolCallParser)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

Multi-provider tool call parsing and normalization for LLM applications.

## Features

- **Unified Interface** - Parse tool calls from any LLM provider into a common format
- **Auto-Detection** - Automatically detect the provider from response format
- **20+ Providers** - OpenAI, Anthropic, Google, xAI, Mistral, Cohere, DeepSeek, AWS Bedrock, and more
- **OpenSource Support** - Ollama, vLLM, GpuStack, Qwen, LMStudio, LocalAI, TGI
- **Result Formatting** - Format tool results back to provider-specific format
- **Extensible** - Register custom parsers for new providers

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
// OpenAI-compatible (Mistral, xAI, DeepSeek, Ollama, etc.)
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
| OpenAI | `tool_calls` | [docs](https://platform.openai.com/docs/guides/function-calling) |
| Azure OpenAI | OpenAI-compatible | [docs](https://learn.microsoft.com/azure/ai-services/openai/) |
| Anthropic (Claude) | `tool_use` blocks | [docs](https://docs.anthropic.com/en/docs/build-with-claude/tool-use) |
| Google Gemini | `functionCall` | [docs](https://ai.google.dev/gemini-api/docs/function-calling) |
| xAI (Grok) | OpenAI-compatible | [docs](https://docs.x.ai/docs/guides/function-calling) |
| Mistral | OpenAI-compatible | [docs](https://docs.mistral.ai/capabilities/function_calling) |
| Cohere | Unique format | [docs](https://docs.cohere.com/docs/tool-use-overview) |
| DeepSeek | OpenAI-compatible | [docs](https://api-docs.deepseek.com/guides/function_calling) |
| AWS Bedrock | `toolUse` blocks | [docs](https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html) |

### Open Source / Self-Hosted

| Provider | Format | Documentation |
|----------|--------|---------------|
| Ollama | OpenAI-compatible | [docs](https://ollama.com/blog/tool-support) |
| GpuStack | OpenAI-compatible | [docs](https://docs.gpustack.ai/) |
| vLLM | OpenAI-compatible | [docs](https://docs.vllm.ai/en/latest/features/tool_calling/) |
| Qwen | OpenAI-compatible | [docs](https://qwen.readthedocs.io/en/latest/framework/function_call.html) |
| LMStudio | OpenAI-compatible | - |
| LocalAI | OpenAI-compatible | - |
| TGI (HuggingFace) | OpenAI-compatible | - |

### Format Categories

```csharp
// Check format compatibility
var provider = Provider.Ollama;

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

## Working with Arguments

```csharp
var toolCall = toolCalls[0];

// Get a specific argument
var location = toolCall.GetArgument<string>("location");

// Get all arguments as a typed object
var args = toolCall.GetArguments<WeatherArgs>();

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

// Format for Google Gemini
var geminiParser = ToolCallParserFactory.GetParser(Provider.Google);
var geminiResults = geminiParser.FormatResults(results);
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
    case Provider.Auto:
        Console.WriteLine("Could not auto-detect");
        break;
}
```

## Custom Parser Registration

```csharp
// Create a custom parser
public class CustomProviderParser : IToolCallParser
{
    public Provider Provider => Provider.OpenAICompatible;
    public IReadOnlyList<ToolCall> Parse(string response) { /* ... */ }
    public IReadOnlyList<ToolCall> Parse(JsonElement element) { /* ... */ }
    public bool HasToolCalls(string response) { /* ... */ }
    public bool HasToolCalls(JsonElement element) { /* ... */ }
    public string FormatResults(IEnumerable<ToolCallResult> results) { /* ... */ }
}

// Register it
ToolCallParserFactory.RegisterParser(Provider.OpenAICompatible, new CustomProviderParser());
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

- .NET 8.0, 9.0, or 10.0
- System.Text.Json (included in .NET)

## Documentation

- [Tool Call Format Guide](docs/tool-call-format-guide.md) - Detailed format specifications for each provider

## Related Projects

- [ironhive-cli](https://github.com/iyulab/ironhive-cli) - CLI agent using ToolCallParser
- [TokenMeter](https://github.com/iyulab/TokenMeter) - Token counting and cost calculation

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
