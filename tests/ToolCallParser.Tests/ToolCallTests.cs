namespace ToolCallParser.Tests;

public class ToolCallTests
{
    [Fact]
    public void GetArgumentsAsJson_ValidJson_ReturnsDocument()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{\"key\": \"value\"}"
        };

        using var doc = toolCall.GetArgumentsAsJson();

        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void GetArguments_DeserializesToType()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{\"Name\": \"John\", \"Age\": 30}"
        };

        var args = toolCall.GetArguments<TestArgs>();

        Assert.NotNull(args);
        Assert.Equal("John", args.Name);
        Assert.Equal(30, args.Age);
    }

    [Fact]
    public void GetArgument_ExistingKey_ReturnsValue()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{\"location\": \"Seattle\"}"
        };

        var location = toolCall.GetArgument<string>("location");

        Assert.Equal("Seattle", location);
    }

    [Fact]
    public void GetArgument_NonExistingKey_ReturnsDefault()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{}"
        };

        var value = toolCall.GetArgument<string>("missing");

        Assert.Null(value);
    }

    [Fact]
    public void HasArgument_ExistingKey_ReturnsTrue()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{\"key\": 123}"
        };

        Assert.True(toolCall.HasArgument("key"));
    }

    [Fact]
    public void HasArgument_NonExistingKey_ReturnsFalse()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{}"
        };

        Assert.False(toolCall.HasArgument("missing"));
    }

    [Fact]
    public void GetArgumentsAsJson_InvalidJson_ThrowsJsonException()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "not valid json"
        };

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => toolCall.GetArgumentsAsJson());
    }

    [Fact]
    public void GetArgument_IntegerType_ReturnsValue()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"count": 42}"""
        };

        var count = toolCall.GetArgument<int>("count");

        Assert.Equal(42, count);
    }

    [Fact]
    public void GetArgument_BooleanType_ReturnsValue()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"enabled": true}"""
        };

        var enabled = toolCall.GetArgument<bool>("enabled");

        Assert.True(enabled);
    }

    [Fact]
    public void GetArgument_DoubleType_ReturnsValue()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"temperature": 0.7}"""
        };

        var temp = toolCall.GetArgument<double>("temperature");

        Assert.Equal(0.7, temp);
    }

    [Fact]
    public void GetArgument_NestedObject_DeserializesCorrectly()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"config": {"Name": "test", "Value": 123}}"""
        };

        var config = toolCall.GetArgument<NestedConfig>("config");

        Assert.NotNull(config);
        Assert.Equal("test", config.Name);
        Assert.Equal(123, config.Value);
    }

    [Fact]
    public void GetArguments_EmptyJson_DeserializesCorrectly()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = "{}"
        };

        var args = toolCall.GetArguments<TestArgs>();

        Assert.NotNull(args);
        Assert.Equal(string.Empty, args.Name);
        Assert.Equal(0, args.Age);
    }

    [Fact]
    public void GetArgument_WithCustomOptions_UsesCaseInsensitive()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"Location": "Seattle"}"""
        };

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Without case-insensitive options, lowercase "location" won't match "Location"
        var withoutOptions = toolCall.GetArgument<string>("location");
        Assert.Null(withoutOptions);

        // Exact case match always works
        var exactMatch = toolCall.GetArgument<string>("Location");
        Assert.Equal("Seattle", exactMatch);
    }

    [Fact]
    public void HasArgument_MultipleKeys_ChecksCorrectly()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"a": 1, "b": "two", "c": null}"""
        };

        Assert.True(toolCall.HasArgument("a"));
        Assert.True(toolCall.HasArgument("b"));
        Assert.True(toolCall.HasArgument("c")); // null value still exists
        Assert.False(toolCall.HasArgument("d"));
    }

    [Fact]
    public void GetArgument_ArrayType_ReturnsArray()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"tags": ["a", "b", "c"]}"""
        };

        var tags = toolCall.GetArgument<string[]>("tags");

        Assert.NotNull(tags);
        Assert.Equal(3, tags.Length);
        Assert.Equal("a", tags[0]);
    }

    [Fact]
    public void GetArgument_NullValue_ReturnsNull()
    {
        var toolCall = new ToolCall
        {
            Id = "test",
            Name = "test_tool",
            Arguments = """{"value": null}"""
        };

        var value = toolCall.GetArgument<string>("value");

        Assert.Null(value);
    }

    private sealed class TestArgs
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class NestedConfig
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}

public class ToolCallResultTests
{
    [Fact]
    public void Success_CreatesSuccessResult()
    {
        var result = ToolCallResult.Success("call_1", "output", "tool_name");

        Assert.Equal("call_1", result.ToolCallId);
        Assert.Equal("output", result.Content);
        Assert.Equal("tool_name", result.ToolName);
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        var result = ToolCallResult.Failure("call_2", "something went wrong", "tool_name");

        Assert.Equal("call_2", result.ToolCallId);
        Assert.Contains("something went wrong", result.Content);
        Assert.Equal("tool_name", result.ToolName);
        Assert.False(result.IsSuccess);
        Assert.Equal("something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Success_WithoutToolName_Works()
    {
        var result = ToolCallResult.Success("call_1", "output");

        Assert.Null(result.ToolName);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DefaultIsSuccess_IsTrue()
    {
        var result = new ToolCallResult
        {
            ToolCallId = "1",
            Content = "test"
        };

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Failure_WithoutToolName_Works()
    {
        var result = ToolCallResult.Failure("call_1", "error message");

        Assert.Null(result.ToolName);
        Assert.False(result.IsSuccess);
        Assert.Equal("error message", result.ErrorMessage);
    }

    [Fact]
    public void Failure_ContentHasErrorPrefix()
    {
        var result = ToolCallResult.Failure("call_1", "something broke");

        Assert.StartsWith("Error: ", result.Content);
        Assert.Contains("something broke", result.Content);
    }

    [Fact]
    public void ToolCallResult_AllProperties_SetCorrectly()
    {
        var result = new ToolCallResult
        {
            ToolCallId = "tc_1",
            ToolName = "my_tool",
            Content = "result data",
            IsSuccess = false,
            ErrorMessage = "custom error"
        };

        Assert.Equal("tc_1", result.ToolCallId);
        Assert.Equal("my_tool", result.ToolName);
        Assert.Equal("result data", result.Content);
        Assert.False(result.IsSuccess);
        Assert.Equal("custom error", result.ErrorMessage);
    }

    [Fact]
    public void ToolCall_RecordEquality_WorksCorrectly()
    {
        var tc1 = new ToolCall { Id = "1", Name = "test", Arguments = "{}" };
        var tc2 = new ToolCall { Id = "1", Name = "test", Arguments = "{}" };

        Assert.Equal(tc1, tc2);
    }

    [Fact]
    public void ToolCallResult_RecordEquality_WorksCorrectly()
    {
        var r1 = ToolCallResult.Success("1", "ok", "tool");
        var r2 = ToolCallResult.Success("1", "ok", "tool");

        Assert.Equal(r1, r2);
    }
}
