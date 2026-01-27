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

    private sealed class TestArgs
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
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
}
