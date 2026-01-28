# Tool Call Format Guide

이 문서는 각 LLM 공급자의 tool calling 형식을 정리하고, 새 공급자 추가 시 참고할 수 있는 가이드입니다.

## 공식 문서 링크

### 상용 클라우드 공급자

| 공급자 | 문서 URL |
|--------|----------|
| OpenAI | https://platform.openai.com/docs/guides/function-calling |
| Azure OpenAI | https://learn.microsoft.com/azure/ai-services/openai/how-to/function-calling |
| Anthropic | https://docs.anthropic.com/en/docs/build-with-claude/tool-use |
| Google Gemini | https://ai.google.dev/gemini-api/docs/function-calling |
| xAI (Grok) | https://docs.x.ai/docs/guides/function-calling |
| Mistral | https://docs.mistral.ai/capabilities/function_calling |
| Cohere | https://docs.cohere.com/docs/tool-use-overview |
| DeepSeek | https://api-docs.deepseek.com/guides/function_calling |
| AWS Bedrock | https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html |

### 오픈소스 / 셀프호스팅

| 공급자 | 문서 URL |
|--------|----------|
| Ollama | https://ollama.com/blog/tool-support |
| vLLM | https://docs.vllm.ai/en/latest/features/tool_calling/ |
| Qwen | https://qwen.readthedocs.io/en/latest/framework/function_call.html |
| GpuStack | https://docs.gpustack.ai/ |

---

## 형식별 상세 사양

### 1. OpenAI 형식 (가장 일반적)

**사용 공급자**: OpenAI, Azure, Mistral, xAI, DeepSeek, Ollama, GpuStack, vLLM, Qwen, LMStudio, LocalAI, TGI

#### Tool 정의

```json
{
  "type": "function",
  "function": {
    "name": "get_weather",
    "description": "Get the current weather in a given location",
    "parameters": {
      "type": "object",
      "properties": {
        "location": {
          "type": "string",
          "description": "The city and state, e.g. San Francisco, CA"
        },
        "unit": {
          "type": "string",
          "enum": ["celsius", "fahrenheit"]
        }
      },
      "required": ["location"]
    }
  }
}
```

#### 응답 형식 (tool_calls)

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": null,
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "get_weather",
          "arguments": "{\"location\": \"San Francisco, CA\", \"unit\": \"celsius\"}"
        }
      }]
    },
    "finish_reason": "tool_calls"
  }]
}
```

#### 레거시 형식 (function_call)

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "function_call": {
        "name": "get_weather",
        "arguments": "{\"location\": \"San Francisco, CA\"}"
      }
    },
    "finish_reason": "function_call"
  }]
}
```

#### Tool 결과 전송

```json
{
  "role": "tool",
  "tool_call_id": "call_abc123",
  "content": "Temperature: 22°C, Sunny"
}
```

---

### 2. Anthropic 형식 (Claude)

**사용 공급자**: Anthropic Claude

#### Tool 정의

```json
{
  "name": "get_weather",
  "description": "Get the current weather in a given location",
  "input_schema": {
    "type": "object",
    "properties": {
      "location": {
        "type": "string",
        "description": "The city and state"
      }
    },
    "required": ["location"]
  }
}
```

#### 응답 형식 (tool_use content block)

```json
{
  "content": [
    {
      "type": "text",
      "text": "I'll check the weather for you."
    },
    {
      "type": "tool_use",
      "id": "toolu_01XYZ",
      "name": "get_weather",
      "input": {
        "location": "San Francisco, CA"
      }
    }
  ],
  "stop_reason": "tool_use"
}
```

#### Tool 결과 전송

```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_01XYZ",
      "content": "Temperature: 22°C, Sunny"
    }
  ]
}
```

---

### 3. Google Gemini 형식

**사용 공급자**: Google Gemini, Vertex AI

#### Tool 정의

```json
{
  "function_declarations": [{
    "name": "get_weather",
    "description": "Get the current weather",
    "parameters": {
      "type": "object",
      "properties": {
        "location": {
          "type": "string",
          "description": "The city name"
        }
      },
      "required": ["location"]
    }
  }]
}
```

#### 응답 형식 (functionCall)

```json
{
  "candidates": [{
    "content": {
      "parts": [{
        "functionCall": {
          "name": "get_weather",
          "args": {
            "location": "Tokyo"
          }
        }
      }],
      "role": "model"
    },
    "finishReason": "STOP"
  }]
}
```

#### Tool 결과 전송

```json
{
  "parts": [{
    "functionResponse": {
      "name": "get_weather",
      "response": {
        "result": "Temperature: 25°C, Cloudy"
      }
    }
  }]
}
```

---

### 4. AWS Bedrock 형식 (Converse API)

**사용 공급자**: AWS Bedrock

#### Tool 정의

```json
{
  "tools": [{
    "toolSpec": {
      "name": "get_weather",
      "description": "Get the current weather",
      "inputSchema": {
        "json": {
          "type": "object",
          "properties": {
            "location": {
              "type": "string"
            }
          }
        }
      }
    }
  }]
}
```

#### 응답 형식 (toolUse)

```json
{
  "output": {
    "message": {
      "role": "assistant",
      "content": [{
        "toolUse": {
          "toolUseId": "tooluse_abc123",
          "name": "get_weather",
          "input": {
            "location": "New York"
          }
        }
      }]
    }
  },
  "stopReason": "tool_use"
}
```

#### Tool 결과 전송

```json
{
  "role": "user",
  "content": [{
    "toolResult": {
      "toolUseId": "tooluse_abc123",
      "content": [{
        "json": {
          "temperature": "20°C",
          "condition": "Rainy"
        }
      }]
    }
  }]
}
```

---

### 5. Cohere 형식 (Command R)

**사용 공급자**: Cohere

#### Tool 정의 (V2 API)

```json
{
  "type": "function",
  "function": {
    "name": "get_weather",
    "description": "Get the current weather",
    "parameters": {
      "type": "object",
      "properties": {
        "location": { "type": "string" }
      },
      "required": ["location"]
    }
  }
}
```

#### 응답 형식

```json
{
  "finish_reason": "TOOL_CALL",
  "message": {
    "tool_calls": [{
      "id": "call_xyz",
      "type": "function",
      "function": {
        "name": "get_weather",
        "arguments": "{\"location\": \"Paris\"}"
      }
    }]
  }
}
```

#### Tool 결과 전송

```json
{
  "tool_results": [{
    "call": {
      "name": "get_weather",
      "parameters": { "location": "Paris" }
    },
    "outputs": [{ "temperature": "18°C" }]
  }]
}
```

---

## 새 공급자 추가 절차

### 1. 형식 분석

새 공급자의 tool calling 형식이 어떤 카테고리에 속하는지 확인:

- **OpenAI-compatible**: `tool_calls` 배열, `function.name`, `function.arguments`
- **Anthropic-compatible**: `tool_use` content block, `input` 객체
- **Unique format**: 새 파서 필요

### 2. Provider.cs 업데이트

```csharp
/// <summary>
/// NewProvider API format.
/// https://docs.newprovider.com/tool-calling
/// </summary>
NewProvider,
```

### 3. 기존 형식 호환 시

ToolCallParserFactory.cs에 매핑 추가:

```csharp
{ Provider.NewProvider, new OpenAIToolCallParser() },
```

### 4. 새 파서 필요 시

1. `Parsers/NewProviderToolCallParser.cs` 생성
2. `IToolCallParser` 인터페이스 구현
3. ToolCallParserFactory에 등록
4. DetectProvider 로직에 감지 규칙 추가

### 5. 테스트 추가

`tests/ToolCallParser.Tests/NewProviderToolCallParserTests.cs`:

```csharp
public class NewProviderToolCallParserTests
{
    [Fact]
    public void Parse_ValidResponse_ReturnsToolCalls()
    {
        var response = """{ ... }""";
        var parser = ToolCallParserFactory.GetParser(Provider.NewProvider);
        var calls = parser.Parse(response);
        Assert.Single(calls);
        Assert.Equal("function_name", calls[0].Name);
    }
}
```

---

## 업데이트 체크리스트

### 월간 점검

- [ ] OpenAI API 변경사항 확인
- [ ] Anthropic API 변경사항 확인
- [ ] Google Gemini API 변경사항 확인
- [ ] 기타 주요 공급자 변경사항 확인

### 주요 릴리스 시

- [ ] 새 모델의 tool calling 지원 확인
- [ ] 형식 변경 여부 테스트
- [ ] 호환성 테스트 실행

---

## 형식 비교 요약

| 공급자 | Tool Call 필드 | Arguments 형식 | ID 필드 | 종료 조건 |
|--------|---------------|---------------|---------|----------|
| OpenAI | `tool_calls` | JSON string | `id` | `finish_reason: "tool_calls"` |
| Anthropic | `content[].tool_use` | Object | `id` | `stop_reason: "tool_use"` |
| Google | `parts[].functionCall` | Object (`args`) | 없음 (생성 필요) | `finishReason` |
| Bedrock | `content[].toolUse` | Object (`input`) | `toolUseId` | `stopReason: "tool_use"` |
| Cohere | `tool_calls` 또는 `actions` | JSON string 또는 Object | `id` 또는 없음 | `finish_reason: "TOOL_CALL"` |

---

## 참고 자료

### API 호환성 테스트 도구

- [LiteLLM](https://docs.litellm.ai/) - 여러 공급자 통합 테스트
- [Promptfoo](https://www.promptfoo.dev/) - LLM 테스트 프레임워크

### 커뮤니티 리소스

- [LangChain Tool Calling](https://python.langchain.com/docs/concepts/tool_calling/)
- [LlamaIndex Tools](https://docs.llamaindex.ai/en/stable/module_guides/deploying/agents/tools/)

---

Last Updated: 2026-01-28
