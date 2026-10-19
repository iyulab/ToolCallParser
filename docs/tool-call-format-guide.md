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

#### Interactions API 형식 (function_call step)

Interactions API 는 2026-06 부로 GA 이며 신규 프로젝트 권장 표면이다. generateContent 는 계속
지원되지만 legacy 로 분류되고, 신규 도구·에이전트 기능은 Interactions 쪽에서 출시된다.

응답은 `candidates[].content.parts[]` 가 아니라 **`steps[]`** 배열이며, 사고·도구호출·도구결과·
최종출력이 시간순으로 섞여 담긴다. 도구 호출 step 은 `type: "function_call"` 로 판별한다.

```json
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
    { "type": "model_output", "content": [{ "text": "..." }] }
  ]
}
```

generateContent 형식과 다른 점 셋:

| | generateContent | Interactions |
|---|---|---|
| 컨테이너 | `candidates[].content.parts[].functionCall` | `steps[]` (`type == "function_call"`) |
| 인자 필드 | `args` | `arguments` |
| 호출 id | **없음** (파서가 생성) | **`id` 제공** — 결과 제출 시 참조하므로 보존한다 |

> **`steps` 봉투만 인식한다.** step 하나를 벗겨 단독으로 넘기면 OpenAI Responses 의
> `function_call` 아이템과 형태가 같아(둘 다 `type`+`name` 객체) 구분이 불가능하다. 단독 step 까지
> 여기서 주장하면 커버리지가 늘어나는 게 아니라 감지가 모호해진다.
>
> **판별자는 `steps` 라는 컨테이너 이름 자체다.** Google 은 감지 순서상 OpenAI 보다 먼저 프로브되므로,
> 다른 provider 가 최상위 `steps` 배열을 도입하면 그 응답을 Google 이 가져가게 된다. 현재 다섯 포맷
> 어디에도 최상위 `steps` 는 없다(2026-08-02 실측). 이 전제가 깨지면 판별자를 좁혀야 한다.

#### Interactions 결과 전송 (function_result)

**0.6.0 부터 왕복이 닫힌다.** 표면을 인자로 받는 오버로드를 쓴다:

```csharp
var parser = new GoogleToolCallParser();
var calls = parser.Parse(interactionsResponse);          // step id 보존
var results = new[] { ToolCallResult.Success(calls[0].Id, """{"tempC":12}""", calls[0].Name) };

var payload = parser.FormatResults(results, GoogleSurface.Interactions);
```

```json
[{
  "type": "function_result",
  "call_id": "gth23981",
  "name": "get_weather",
  "result": { "tempC": 12 },
  "is_error": false
}]
```

- `call_id` 는 **파싱 시 보존한 step `id`** 다. 여기서 생성 Guid 로 갈아 끼우면 Google 쪽 매칭이 깨진다.
- `result` 는 **항상 객체**다. 내용이 이미 JSON 객체면 그대로 싣고, 아니면 `{"result": "<텍스트>"}` 로 감싼다.
- `is_error` 는 `ToolCallResult.IsSuccess` 의 반대다. 실패 시 `result` 에는 `Content` 가 실린다
  (`ToolCallResult.Failure` 가 `"Error: <메시지>"` 로 채우는 그 값).

> **왜 인자가 필요한가** — 읽는 쪽은 형태로 표면을 구분할 수 있지만(`steps` vs `candidates`),
> 쓰는 쪽은 구분할 근거가 없다. 결과 목록에는 목적지 정보가 없고 `IToolCallParser.FormatResults`
> 시그니처에도 없다. **호출자만 아는 정보이므로 호출자가 말한다.** 무인자 형태는 종전대로
> generateContent 를 낸다(아래).

#### generateContent 결과 전송

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

#### OpenAI 호환 표면 (Chat Completions · Responses)

Bedrock 은 위 Converse 형식 외에 **OpenAI 호환 엔드포인트**(`bedrock-mantle`)로 Chat Completions 와
Responses 를 함께 제공한다. 벤더 문서가 wire 스펙을 OpenAI 문서로 그대로 위임하고 "base URL 만
바꾸면 된다"고 명시하므로, 이 표면의 응답은 **OpenAI 포맷 그 자체**다.

| 표면 | 컨테이너 | 파서 | `DetectProvider` |
|---|---|---|---|
| Converse | `output.message.content[].toolUse` (객체) | `BedrockToolCallParser` | `Provider.Bedrock` |
| Chat Completions | `choices[].message.tool_calls[]` | `OpenAIToolCallParser` | `Provider.OpenAI` |
| Responses | `output[]` 의 `type:"function_call"` (**배열**) | `OpenAIToolCallParser` | `Provider.OpenAI` |

**어트리뷰션은 의도된 동작이다** — 셀프호스팅 런타임과 같은 규칙(OpenAI wire 를 쓰면 OpenAI 포맷으로
분류)이다. 벤더를 이름으로 되돌려 주지는 않는다: 페이로드에 그 정보가 없다.

> ⚠️ **`output` 은 두 표면이 서로 다른 kind 로 쓰는 이름이다** — Converse 는 객체, Responses 는 배열.
> 감지는 어느 쪽이 올지 모르는 상태에서 도는 것이 존재 이유이므로, 프로브는 모르는 형태에 대해
> **"내 것이 아니다"라고 답할 수 있어야 하고 절대 던지면 안 된다**. `JsonElement.TryGetProperty` 는
> `Try` 접두사와 달리 **전 함수가 아니라** 수신자가 객체가 아니면 던지므로, 형태가 확정되지 않은
> 수신자에는 `JsonElementExtensions.TryGetObjectProperty` 를 쓴다. 회귀는
> `ShapeMismatchRobustnessTests` 가 **등록된 전 파서 × 형태 불일치 16종**으로 고정한다 — 새 파서를
> 팩토리에 넣으면 자동으로 포함된다.

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

### 1. 벤더 드리프트 확인 (주기 점검)

- [ ] OpenAI · Anthropic · Google · Cohere · Bedrock 각 function-calling 문서 변경 확인
- [ ] **provider 가 아니라 표면 단위로 본다** — 한 벤더가 표면을 둘 이상 운영하는 것이 이제 통상이다
      (OpenAI: Chat Completions + Responses / Google: generateContent + Interactions /
      Bedrock: Converse + OpenAI 호환). **새 표면은 기존 표면을 대체하지 않고 병존**하며, 신규 기능이
      새 표면에만 실리는 동안 구 표면이 legacy 로 밀린다
- [ ] 새 모델이 특정 표면 전용으로만 나오는지 확인(예: Responses 전용 모델)

### 2. 표면을 추가하거나 고칠 때 — 전 경로를 같이 움직인다

> 이 절은 **실측으로 얻은 것**이다. 2026-08-02 회차가 찾은 결함 4건은 **한 건도 벤더 드리프트가
> 아니었고**, 전부 "새 표면을 일부 경로에만 추가했다"였다. 위 1번만 수행하면 이 클래스는 안 잡힌다.

한 표면을 건드리면 **다섯 자리가 전부** 갱신돼야 한다. 하나라도 빠지면 조용히 어긋난다:

- [ ] `CanParse` — 감지. 빠지면 다른 파서가 가져가고 **호출이 조용히 사라진다**(0.3.1 · 0.6.0 실례)
- [ ] `Parse` — 추출
- [ ] `HasToolCalls` — 존재 확인. 빠지면 `if (HasToolCalls) Parse` 가드를 쓰는 호출자가 **통째로 건너뛴다**
- [ ] `FormatResults` — 결과 반환. 빠지면 **읽을 수는 있는데 답할 수 없다**(0.3.0 D1 · 0.6.0 실례)
- [ ] **테스트 두 곳에 행 추가** — 아래

**행을 추가할 두 테스트** (파서 직접 호출 테스트만 늘리면 위 결함들이 **구조적으로 안 보인다**):

- [ ] `WireSurfaceMatrixTests` — 팩토리 경유 감지·파싱·call id
- [ ] `RoundTripCallIdTests` — 파싱한 id 를 그대로 포맷에 넣어 되돌아오는지
      (id 를 **하드코딩하면 왕복이 아니라 포맷터만 재게 된다**)

프로브는 자기 것이 아닌 형태에 **절대 던지면 안 된다** — 형태가 확정되지 않은 수신자에는
`JsonElementExtensions.TryGetObjectProperty` 를 쓴다. `ShapeMismatchRobustnessTests` 가 전 파서 ×
형태 불일치로 이 선을 지킨다(새 파서는 팩토리 등록만으로 자동 포함).

---

## 형식 비교 요약

**provider 가 아니라 표면 단위로 읽는다** — 같은 벤더의 두 표면이 서로 다른 행을 갖는다.

| 공급자 / 표면 | Tool Call 필드 | Arguments 형식 | ID 필드 | 결과 반환 시 ID |
|---|---|---|---|---|
| OpenAI · Chat Completions | `choices[].message.tool_calls` | JSON string | `id` | `tool_call_id` |
| OpenAI · Responses | `output[]` `type:"function_call"` | JSON string 또는 Object | `call_id` (없으면 `id`) | `tool_call_id` |
| Anthropic · Messages | `content[].tool_use` | Object (`input`) | `id` | `tool_use_id` |
| Google · generateContent | `parts[].functionCall` | Object (`args`) | **없음 (생성)** | **없음** — 이름·순서 매칭 |
| Google · Interactions | `steps[]` `type:"function_call"` | Object (`arguments`) | `id` | `call_id` |
| Bedrock · Converse | `content[].toolUse` | Object (`input`) | `toolUseId` | `toolUseId` |
| Bedrock · OpenAI 호환 | (OpenAI 행과 동일) | — | — | — |
| Cohere · v2 | `message.tool_calls[].function` | JSON string | `id` | `tool_call_id` |
| Cohere · v1 | `message.tool_calls[]` bare `{name, parameters}` 또는 `actions[]` | Object | **없음 (생성)** | `tool_call_id` |

종료 조건: OpenAI `finish_reason:"tool_calls"` · Anthropic `stop_reason:"tool_use"` ·
Google `finishReason` · Bedrock `stopReason:"tool_use"` · Cohere `finish_reason:"TOOL_CALL"`.
**감지가 종료 조건에만 의존하지는 않는다** — 종료 조건이 없는 응답도 형태로 판별한다.

---

## 참고 자료

### API 호환성 테스트 도구

- [LiteLLM](https://docs.litellm.ai/) - 여러 공급자 통합 테스트
- [Promptfoo](https://www.promptfoo.dev/) - LLM 테스트 프레임워크

### 커뮤니티 리소스

- [LangChain Tool Calling](https://python.langchain.com/docs/concepts/tool_calling/)
- [LlamaIndex Tools](https://docs.llamaindex.ai/en/stable/module_guides/deploying/agents/tools/)

---

Release history: [CHANGELOG.md](../CHANGELOG.md).
