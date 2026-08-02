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

> 이 절은 **실측으로 얻은 것**이다. 2026-08-03 회차가 찾은 결함 4건은 **한 건도 벤더 드리프트가
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

## 드리프트 점검 로그

| 점검일 | 결과 | 비고 |
|--------|------|------|
| 2026-07-06 | 드리프트 없음 | 5개 canonical 포맷(OpenAI/Anthropic/Google/Bedrock/Cohere) 및 `ToolCallParserFactory` 감지 규칙이 현행 provider 포맷과 일치. TokenMeter 0.4.1에서 추가된 신규 모델(Claude Opus 4.8/Sonnet 5/Fable 5, GPT-5.5, Grok 4.3, Gemini 3.5)은 모두 각 family의 기존 wire 포맷 재사용 → 파서 변경 불필요. Anthropic 문서 host 이전(docs.anthropic.com→platform.claude.com) 반영. |
| 2026-07-07 | **0.3.0 breaking** | 코드 품질 리뷰 후속(D1~D4). **D1**: Cohere `FormatResults`를 v1 `tool_results`에서 v2 `{role:"tool", tool_call_id, content}`로 전환(parse↔format 정합, tool_call_id 보존). **D3**: 포맷 감지를 `IToolCallParser.CanParse`(default interface method)로 단일 원천화 — `Factory.Is*Format` 5종 제거. **D2**: 파서 선택을 바꾸지 않던 self-host `Provider` 값 7종(Ollama/GpuStack/VLLM/Qwen/LMStudio/LocalAI/TGI) 제거 → `OpenAICompatible` 사용. **D4**: 미사용 `ToolCallParserFactory.RegisterParser` 제거(전역 mutation footgun), 내부 parser 테이블 `FrozenDictionary`화. |
| 2026-07-21 | **0.4.0 additive** | 격주 점검. **OpenAI Responses API `function_call` output item 파싱 추가** — Responses-전용 모델(gpt-5.4-pro 등) 등장으로 커버리지 갭이 실사용 갭이 됨. `call_id`→Id 매핑, object-형 arguments 관용 처리, 회귀 7종. 나머지 4 provider 무드리프트(Anthropic tool_use·Cohere v2·Gemini generateContent functionCall·Bedrock Converse toolUse 불변). **관찰(비조치)**: ① Google **Interactions API**(신규)가 `type:'function_call'` step + object arguments 사용 — generateContent와 병존, 파서 어트리뷰션 검토 필요 시 후속. ② Bedrock이 Responses/Chat Completions 모드 추가 — OpenAI-호환 표면이라 기존 파서로 커버 추정, 실측은 후속. ③ OpenAI custom tools(`custom_tool_call`, plain-text arguments) 미지원 유지 — 수요 신호 대기. 부수: repo nuget.config 신설(NU1507 — 머신 레벨 소스 누수 차단, iron-prow 선례). |
| 2026-07-07 | **0.3.1 fix (additive)** | 항목 5(silent-drop 갭). `CohereToolCallParser.CanParse`가 `finish_reason`/`tool_plan`/`actions` 없는 **레거시 Cohere V1** bare `tool_calls:[{name, parameters}]` shape를 감지하도록 보강. 이전엔 이 응답이 OpenAI 파서로 오라우팅되어 `{name,parameters}`를 못 읽고 도구 호출이 조용히 사라졌음(파서 `Parse`는 이미 V1 처리 가능했으나 자동감지가 못 미침). 판별자=bare `name` + `function` 미존재(OpenAI/V2는 `function` 래핑이라 충돌 없음). 회귀 테스트 3종 추가. |

| 2026-08-02 | **0.5.0 additive** | 격주 점검. 직전 회차가 **관찰(비조치)**로 넘긴 ①번을 실측했더니 조용한 소실이었다. **Google Interactions API `steps[]` 파싱 추가** — Interactions 는 2026-06 GA·신규 프로젝트 권장이고 신규 도구/에이전트 기능이 여기서 출시되는 반면 generateContent 는 legacy 로 분류됐다. 즉 Google 커버리지가 레거시 표면만 덮고 있었다. 실측: `{"steps":[{"type":"function_call",...}]}` 에 대해 Google `CanParse` 는 `functionCall` 만 보므로 false, OpenAI 쪽 Responses 감지는 컨테이너가 `output[]` 이라 false → **`DetectProvider` = `Auto`, `Parse` = 빈 배열, 예외 없음**. 0.3.1 Cohere V1 과 동일한 silent-drop 클래스. 처방은 `steps` 봉투 인식 + `arguments`(객체) + **`id` 보존**(generateContent 와 달리 Interactions 는 결과 제출용 id 를 준다 — 생성 Guid 로 덮으면 왕복이 깨진다). 회귀 8종, 216 GREEN. **의도적 비확장**: 단독 step 은 OpenAI Responses 아이템과 형태가 같아(둘 다 `type`+`name`) 주장하지 않는다 — 커버리지가 아니라 모호성이 는다. 나머지 4 provider 무드리프트. **관찰(이월)**: ② Bedrock Responses/Chat Completions 모드 실측 미실시. ③ OpenAI custom tools 미지원 유지(수요 신호 대기). |

| 2026-08-03 | **0.6.0 fix(critical) + additive** | 이월 관찰 ②(Bedrock Responses/Chat Completions 모드) 실측. 결론은 "기존 파서가 커버한다"가 **절반만 맞았다**: 파싱은 되지만 **자동 감지가 예외로 죽고 있었다.** Bedrock 은 `bedrock-mantle` 로 OpenAI 호환 Chat Completions·Responses 를 제공하고 wire 스펙을 OpenAI 문서에 위임한다. 그런데 Responses 의 루트 `output` 은 **배열**인 반면 Converse 의 `output` 은 **객체**이고, `DetectionOrder` 가 Bedrock 을 OpenAI 보다 먼저 프로브한다. `BedrockToolCallParser.CanParse` 가 `output.TryGetProperty("message")` 를 무가드로 호출 → **`InvalidOperationException`**. 즉 **Bedrock 뿐 아니라 OpenAI 자신의 Responses 응답도** `Factory.DetectProvider`/`Parse`/`HasToolCalls` 에서 던지고 있었다. 0.4.0 이 Responses 파싱을 추가하면서 파서 직접 호출로만 검증하고 **팩토리 경유 경로를 한 번도 안 지난 것**이 이 갭을 살려 뒀다(0.5.0 Google 테스트가 초록이었던 건 Google 이 Bedrock 보다 앞서 프로브돼 조기 반환했기 때문). 근본 원인은 Bedrock 한 곳이 아니라 **`JsonElement.TryGetProperty` 가 전 함수가 아니라는 사실**을 코드베이스가 dictionary TryGet 처럼 다룬 것 — 미가드 체인이 5개 파서 전부에 있었고 형태 불일치 16종 × 전 파서에서 **64건 중 50건이 던졌다**. 처방은 가드를 20여 곳에 뿌리는 대신 총함수 헬퍼 `TryGetObjectProperty` 로 수렴(파서 전량 치환). 회귀 73종(팩토리 경유 표면 감지 9 + 형태 불일치 64), 289 GREEN.<br><br>**같은 판에서 wire 표면 단위 커버리지 매트릭스를 세웠고**(벤더가 실제 반환하는 표면 11종 × 팩토리 경유 감지·파싱·call id), 여기서 **Cohere 결함 2건**이 더 나왔다. 팩토리 테스트는 provider 5종 전부 있었지만 **provider 단위로만 있고 표면 단위로는 없었던 것**이 두 결함을 가린 원인이다. ① **V1 `message.tool_calls` 조용한 소실** — 0.3.1 이 V1 bare `{name, parameters}` 감지를 넣었으나 **루트 `tool_calls` 만** 덮었다. V1 이 실제로 쓰는 중첩 형태(`message.tool_calls`, `Parse` 는 이미 읽던 것)는 어느 파서도 감지하지 못해 OpenAI 로 넘어갔고, OpenAI `ParseToolCall` 은 `id`+`function` 을 요구하므로 null → **빈 배열**. 0.3.1 이 고쳤다고 선언한 결함 클래스가 **그 파서 자신의 테스트가 쓰는 shape 에 남아 있었다.** 처방은 bare 판별을 `ContainsBareToolCall` 로 추출해 두 배치 모두에 적용. ② **`CanParse`/`Parse` ↔ `HasToolCalls` 불일치** — `actions[]`(multi-step)를 앞의 둘은 읽는데 `HasToolCalls` 에만 분기가 없어, 문서화된 가드 패턴 `if (HasToolCalls) Parse` 를 따르는 호출자가 actions 응답을 **통째로 건너뛰었다**. 300 GREEN.<br><br>**additive — Interactions 왕복 완결**: 0.5.0 이 읽는 쪽만 열어 둔 것을 닫았다. `GoogleToolCallParser.FormatResults(results, GoogleSurface.Interactions)` 오버로드 신설 — `type:"function_result"` + **`call_id`(파싱 시 보존한 step `id`)** + `name` + `result`(객체) + `is_error`. **대상 표면을 인자로 받는 이유**: 응답 본문에는 어느 표면에서 왔는지 알려주는 표지가 없어서 쓰는 쪽은 호출자만 알 수 있다(읽는 쪽은 형태로 구분되므로 인자가 필요 없다). `IToolCallParser` 계약에는 표면 인자가 없으므로 오버로드는 **인터페이스 밖에 나란히** 두고, 무인자 형태는 종전대로 generateContent 를 낸다(회귀 없음). 0.3.0 D1(Cohere `FormatResults` v1→v2)이 남긴 "읽는 형식과 쓰는 형식은 같이 움직인다" 선례를 이번에 지켰다. 왕복 회귀 10종, **310 GREEN**. **관찰(이월)**: ③ OpenAI custom tools 미지원 유지(수요 신호 대기). |

---

Last Updated: 2026-08-03 (0.6.0)
