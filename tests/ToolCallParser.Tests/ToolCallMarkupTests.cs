using System.Text.Json;
using Xunit;

namespace ToolCallParser.Tests;

/// <summary>
/// Tool calls an open model wrote as text in its chat template's markup are read back as name + arguments, and text that
/// only mentions the markup is not.
/// </summary>
public class ToolCallMarkupTests
{
    private const string HermesBlock =
        "I will save it now.\n<tool_call>\n{\"name\": \"WriteFile\", \"arguments\": {\"path\": \"answer.txt\", \"content\": \"42\"}}\n</tool_call>";

    private const string QwenCoderBlock =
        "<tool_call>\n<function=WriteFile>\n<parameter=path>\nanswer.txt\n</parameter>\n<parameter=content>\nline one\nline two\n</parameter>\n</function>\n</tool_call>";

    [Fact]
    public void AHermesBlock_ReadsNameAndArguments_AndKeepsTheProseBeforeIt()
    {
        var read = ToolCallMarkup.Read(HermesBlock);

        Assert.Equal("I will save it now.", read.Prefix);
        var block = Assert.Single(read.Blocks);
        Assert.Equal(ToolCallMarkupFormat.Json, block.Format);
        Assert.Equal("WriteFile", block.Name);
        using var args = JsonDocument.Parse(block.Arguments!);
        Assert.Equal("answer.txt", args.RootElement.GetProperty("path").GetString());
        Assert.Equal("42", args.RootElement.GetProperty("content").GetString());
        Assert.Null(read.UnterminatedAt);
    }

    [Fact]
    public void ArgumentsGivenAsAJsonString_ReadAsTheObjectTheyHold()
    {
        var read = ToolCallMarkup.Read("<tool_call>{\"name\": \"Repeat\", \"arguments\": \"{\\\"times\\\": 3}\"}</tool_call>");

        using var args = JsonDocument.Parse(Assert.Single(read.Blocks).Arguments!);
        Assert.Equal(3, args.RootElement.GetProperty("times").GetInt32());
    }

    [Theory]
    [InlineData("<tool_call>{\"name\": \"Ping\"}</tool_call>")]
    [InlineData("<tool_call>{\"name\": \"Ping\", \"arguments\": null}</tool_call>")]
    public void ABlockWithoutArguments_ReadsAsAnEmptyObject(string text) =>
        Assert.Equal("{}", Assert.Single(ToolCallMarkup.Read(text).Blocks).Arguments);

    [Fact]
    public void ParametersKeyIsReadLikeArguments() =>
        Assert.Contains("\"q\"", Assert.Single(ToolCallMarkup.Read("<tool_call>{\"name\": \"Find\", \"parameters\": {\"q\": \"x\"}}</tool_call>").Blocks).Arguments);

    [Fact]
    public void AQwenCoderBlock_ReadsParametersAsWritten_WithOneNewlineTrimmedEachSide()
    {
        var block = Assert.Single(ToolCallMarkup.Read(QwenCoderBlock).Blocks);

        Assert.Equal(ToolCallMarkupFormat.Function, block.Format);
        Assert.Equal("WriteFile", block.Name);
        Assert.Null(block.Arguments);
        Assert.Equal(
            [new("path", "answer.txt"), new("content", "line one\nline two")],
            block.Parameters);
    }

    [Fact]
    public void AFunctionBlockWithoutTheWrapper_IsRead()
    {
        var read = ToolCallMarkup.Read("Sure.\n<function=Repeat>\n<parameter=times>\n3\n</parameter>\n</function>");

        Assert.Equal("Sure.", read.Prefix);
        Assert.Equal("Repeat", Assert.Single(read.Blocks).Name);
    }

    [Fact]
    public void AWrapperOpenedButNotClosed_IsPartOfTheCall_NotOfThePrefix()
    {
        var read = ToolCallMarkup.Read("Writing.\n<tool_call>\n<function=WriteFile>\n<parameter=path>\na\n</parameter>\n</function>");

        Assert.Equal("Writing.", read.Prefix);
        Assert.Equal("WriteFile", Assert.Single(read.Blocks).Name);
    }

    [Fact]
    public void SeveralBlocks_AreReadInOrder()
    {
        var read = ToolCallMarkup.Read(
            "<tool_call>{\"name\": \"A\", \"arguments\": {}}</tool_call>\n<tool_call>{\"name\": \"B\", \"arguments\": {}}</tool_call>");

        Assert.Equal(["A", "B"], read.Blocks.Select(b => b.Name));
    }

    [Theory]
    [InlineData("<tool_call>{\"name\": \"WriteFile\", \"arguments\": </tool_call>")] // does not parse
    [InlineData("<tool_call>{\"arguments\": {}}</tool_call>")]                     // no name
    [InlineData("<tool_call>{\"name\": \"A\", \"arguments\": [1]}</tool_call>")]   // arguments not an object
    [InlineData("<tool_call>{\"name\": \"A\", \"arguments\": \"[1]\"}</tool_call>")] // string holding a non-object
    [InlineData("<tool_call>just words</tool_call>")]                             // neither form
    public void ABlockThatDoesNotRead_IsKeptButNotWellFormed(string text)
    {
        var block = Assert.Single(ToolCallMarkup.Read(text).Blocks);

        Assert.False(block.IsWellFormed);
        Assert.Null(block.Name);
    }

    [Theory]
    [InlineData("Done. <tool_call>\n{\"name\": \"WriteFile\", \"argu", 6)]
    [InlineData("Done. <tool_call>", 6)]
    [InlineData("Done. <function=WriteFile>\n<parameter=path>\na", 6)]
    [InlineData("Done. <function=Write", 6)]
    public void TextThatEndsInsideABlock_SaysWhereTheBlockBegins(string text, int start)
    {
        var read = ToolCallMarkup.Read(text);

        Assert.Empty(read.Blocks);
        Assert.Equal(start, read.UnterminatedAt);
        Assert.Equal(text, read.Prefix);
    }

    [Theory]
    [InlineData("The format is <tool_call>, followed by JSON.")]
    [InlineData("Call <function=name> with parameters.")]
    [InlineData("The weather in Tokyo is sunny.")]
    [InlineData("")]
    public void ProseThatOnlyMentionsTheMarkup_HasNoBlocksAndIsNotUnterminated(string text)
    {
        var read = ToolCallMarkup.Read(text);

        Assert.Empty(read.Blocks);
        Assert.Null(read.UnterminatedAt);
        Assert.Equal(text, read.Prefix);
    }

    [Theory]
    [InlineData("plain answer", ToolCallMarkupScan.None)]
    [InlineData("a < b", ToolCallMarkupScan.None)]
    [InlineData("about to <tool_", ToolCallMarkupScan.Partial)]
    [InlineData("about to <", ToolCallMarkupScan.Partial)]
    [InlineData("about to <func", ToolCallMarkupScan.Partial)]
    [InlineData("x <tool_call>{", ToolCallMarkupScan.Found)]
    [InlineData("x <function=A>", ToolCallMarkupScan.Found)]
    public void Scan_TellsAStreamingReaderWhetherToHold(string text, ToolCallMarkupScan expected) =>
        Assert.Equal(expected, ToolCallMarkup.Scan(text));

    [Fact]
    public void Read_RejectsNull() => Assert.Throws<ArgumentNullException>(() => ToolCallMarkup.Read(null!));
}
