using System.Reflection;
using Iyu.Conventions.Testing.Snippets;

namespace ToolCallParser.Tests;

/// <summary>
/// Compiles every C# block in the README against the current assemblies. A block that names a member the library does not
/// have, passes the wrong arguments, or needs a namespace the text does not mention fails here instead of in a reader's
/// editor.
/// </summary>
/// <remarks>
/// Each block is compiled as a top-level program by <see cref="SnippetCompiler"/>: its own usings, the usings below, and the
/// stand-ins below when the block uses a name without declaring it — values a reader already has from the surrounding text,
/// not part of what the block shows. The README reads as one guide across the library's namespaces, so all of them are in
/// scope. A block that is deliberately not a program is listed in <see cref="Fragments"/> under its heading, with the reason.
/// </remarks>
public class ReadmeSnippetCompileTests
{
    // Shrink this, never grow it silently.
    private static readonly SnippetRoster Fragments = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ToolCall"] = "API reference: the type's member signatures, listed without bodies",
        ["ToolCallResult"] = "API reference: the type's member signatures, listed without bodies",
        ["ProviderExtensions"] = "API reference: the type's member signatures, listed without bodies",
    });

    private static readonly string[] CommonUsings =
    [
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading",
        "System.Threading.Tasks", "System.Text.Json",
    ];

    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["jsonResponse"] = "string jsonResponse = \"{}\";",
        ["response"] = "string response = \"{}\";",
        ["toolCalls"] = "IReadOnlyList<ToolCall> toolCalls = [];",
        ["results"] = "ToolCallResult[] results = [];",
    };

    private static readonly Assembly Library = typeof(ToolCallParserFactory).Assembly;

    private static readonly SnippetCompiler Compiler = new(SnippetCompiler.ReferencesFor(Library))
    {
        Usings = [.. CommonUsings, .. Library.GetExportedTypes().Select(t => t.Namespace).OfType<string>().Distinct().Order(StringComparer.Ordinal)],
        StandIns = StandIns,
    };

    private static readonly IReadOnlyList<DocSnippet> Snippets = DocSnippets.Read(Path.Combine(RepoRoot(), "README.md"), "README.md");

    public static TheoryData<string> Blocks() => new(Snippets.Select(s => s.Key));

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadmeBlock_Compiles(string key)
    {
        var snippet = Snippets.Single(s => s.Key == key);
        if (!Fragments.IsFragment(snippet))
        {
            Compiler.ShouldCompile(snippet);
        }
    }

    [Fact]
    public void EveryReadmeBlock_IsFoundAndFragmentsNameRealHeadings() => Fragments.ShouldMatch([.. Snippets], atLeast: 10);

    /// <summary>Positive control: the compiler rejects a call the library does not have.</summary>
    [Fact]
    public void Compile_RejectsAMethodTheLibraryDoesNotHave()
    {
        var errors = Compiler.Compile("""
            var parser = ToolCallParserFactory.GetParser(Provider.OpenAI);
            parser.ParseEverything("{}");
            """);

        Assert.NotEmpty(errors);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ToolCallParser.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("ToolCallParser.slnx not found above the test output directory");
    }
}
