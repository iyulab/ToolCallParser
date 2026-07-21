# Tool Call Format Guide

이 문서는 각 LLM 공급자의 tool calling 형식을 정리하고, 새 공급자 추가 시 참고할 수 있는 가이드입니다.

## 공식 문서 링크

### 상용 클라우드 공급자

| 공급자 | 문서 URL |
|--------|----------|
| OpenAI | https://platform.openai.com/docs/guides/function-calling |
| Azure OpenAI | https://learn.microsoft.com/azure/ai-services/openai/how-to/function-calling |
| Anthropic | https://platform.claude.com/docs/en/docs/build-with-claude/tool-use |
| Google Gemini | https://ai.google.dev/gemini-api/docs/function-calling |
| xAI (Grok) | https://docs.x.ai/docs/guides/function-calling |
| Mistral | https://docs.mistral.ai/capabilities/function_calling |
| Cohere | https://docs.cohere.com/docs/tool-use-overview |
| DeepSeek | https://api-docs.deepseek.com/guides/function_calling |
| AWS Bedrock | https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html |

### 오픈소스 / 셀프호스팅

셀프호스팅 런타임은 OpenAI tool-calling wire 포맷을 사용하므로 런타임별 enum 값 대신 단일 `Provider.OpenAICompatible`로 처리한다(감지 시에도 OpenAI 포맷으로 분류).

| 런타임 (참고) | 문서 URL |
|--------------|----------|
| Ollama | https://ollama.com/blog/tool-support |
| vLLM | https://docs.vllm.ai/en/latest/features/tool_calling/ |
| Qwen | https://qwen.readthedocs.io/en/latest/framework/function_call.html |
| GpuStack | https://docs.gpustack.ai/ |
| LM Studio / LocalAI / TGI | (OpenAI 호환) |

---

## 형식별 상세 사양

### 1. OpenAI 형식 (가장 일반적)

**사용 공급자**: OpenAI, Azure, Mistral, xAI, DeepSeek, 그리고 모든 OpenAI 호환 엔드포인트(`OpenAICompatible` — Ollama/vLLM/LM Studio/LocalAI/TGI/GpuStack/Qwen 등)

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

#### Responses API 형식 (function_call output item)

Responses API는 `choices`/`tool_calls`가 아니라 최상위 `output[]` 배열에 아이템을 담는다.
Responses-전용 모델(예: gpt-5.4-pro)은 이 형식만 반환한다. (0.4.0부터 파싱 지원)

```json
{
  "id": "resp_...",
  "output": [
    {
      "id": "fc_12345xyz",
      "call_id": "call_12345xyz",
      "type": "function_call",
      "name": "get_weather",
      "arguments": "{\"location\":\"Paris, France\"}"
    }
  ]
}
```

- `call_id`가 결과 제출용 참조 id (ToolCall.Id로 매핑, 부재 시 `id` 폴백)
- custom tools는 `type: "custom_tool_call"` + plain-text arguments — **미지원** (수요 시 추가)

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
      "content": "Temperature: 22°C, Sunny",
      "is_error": false
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

#### Tool 결과 전송 (V2 API)

Cohere Chat API **v2**는 결과를 `messages` 배열에 `role: "tool"` 메시지로 넣는다. `tool_call_id`로 원래 호출과 매칭하며, 레거시 v1(`tool_results[].call.parameters`)과 달리 **원본 파라미터를 echo하지 않는다**. `ToolCallParser`의 `CohereToolCallParser.FormatResults`가 방출하는 형식:

```json
[{
  "role": "tool",
  "tool_call_id": "call_xyz",
  "content": "18°C"
}]
```

> 참고: 파서는 v2 tool call(`{ id, type, function }`)을 파싱하므로 결과도 v2 shape로 방출한다(parse↔format 정합). v1 `tool_results` 포맷은 레거시이며 Cohere 마이그레이션 가이드가 v2로 유도한다: https://docs.cohere.com/docs/migrating-v1-to-v2

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

## 드리프트 점검 로그

| 점검일 | 결과 | 비고 |
|--------|------|------|
| 2026-07-06 | 드리프트 없음 | 5개 canonical 포맷(OpenAI/Anthropic/Google/Bedrock/Cohere) 및 `ToolCallParserFactory` 감지 규칙이 현행 provider 포맷과 일치. TokenMeter 0.4.1에서 추가된 신규 모델(Claude Opus 4.8/Sonnet 5/Fable 5, GPT-5.5, Grok 4.3, Gemini 3.5)은 모두 각 family의 기존 wire 포맷 재사용 → 파서 변경 불필요. Anthropic 문서 host 이전(docs.anthropic.com→platform.claude.com) 반영. |
| 2026-07-07 | **0.3.0 breaking** | 코드 품질 리뷰 후속(D1~D4). **D1**: Cohere `FormatResults`를 v1 `tool_results`에서 v2 `{role:"tool", tool_call_id, content}`로 전환(parse↔format 정합, tool_call_id 보존). **D3**: 포맷 감지를 `IToolCallParser.CanParse`(default interface method)로 단일 원천화 — `Factory.Is*Format` 5종 제거. **D2**: 파서 선택을 바꾸지 않던 self-host `Provider` 값 7종(Ollama/GpuStack/VLLM/Qwen/LMStudio/LocalAI/TGI) 제거 → `OpenAICompatible` 사용. **D4**: 미사용 `ToolCallParserFactory.RegisterParser` 제거(전역 mutation footgun), 내부 parser 테이블 `FrozenDictionary`화. |
| 2026-07-21 | **0.4.0 additive** | 격주 점검. **OpenAI Responses API `function_call` output item 파싱 추가** — Responses-전용 모델(gpt-5.4-pro 등) 등장으로 커버리지 갭이 실사용 갭이 됨. `call_id`→Id 매핑, object-형 arguments 관용 처리, 회귀 7종. 나머지 4 provider 무드리프트(Anthropic tool_use·Cohere v2·Gemini generateContent functionCall·Bedrock Converse toolUse 불변). **관찰(비조치)**: ① Google **Interactions API**(신규)가 `type:'function_call'` step + object arguments 사용 — generateContent와 병존, 파서 어트리뷰션 검토 필요 시 후속. ② Bedrock이 Responses/Chat Completions 모드 추가 — OpenAI-호환 표면이라 기존 파서로 커버 추정, 실측은 후속. ③ OpenAI custom tools(`custom_tool_call`, plain-text arguments) 미지원 유지 — 수요 신호 대기. 부수: repo nuget.config 신설(NU1507 — 머신 레벨 소스 누수 차단, iron-prow 선례). |
| 2026-07-07 | **0.3.1 fix (additive)** | 항목 5(silent-drop 갭). `CohereToolCallParser.CanParse`가 `finish_reason`/`tool_plan`/`actions` 없는 **레거시 Cohere V1** bare `tool_calls:[{name, parameters}]` shape를 감지하도록 보강. 이전엔 이 응답이 OpenAI 파서로 오라우팅되어 `{name,parameters}`를 못 읽고 도구 호출이 조용히 사라졌음(파서 `Parse`는 이미 V1 처리 가능했으나 자동감지가 못 미침). 판별자=bare `name` + `function` 미존재(OpenAI/V2는 `function` 래핑이라 충돌 없음). 회귀 테스트 3종 추가. |

---

Last Updated: 2026-07-21 (0.4.0)
