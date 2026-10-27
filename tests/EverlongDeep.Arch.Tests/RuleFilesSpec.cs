namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Rule files load for the files they govern: docs/REVIEW.md → "Agent, rule and skill files". The
/// fixtures live in a temp directory, so Claude Code does not load them as rules.
/// </summary>
public sealed class RuleFilesSpec : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rule-files-").FullName;

    public RuleFilesSpec()
    {
        Directory.CreateDirectory(Rules);
        Directory.CreateDirectory(Path.Combine(_root, "src", "ui"));
        File.WriteAllText(Path.Combine(_root, "project.godot"), "");
        File.WriteAllText(Rule, "---\npaths:\n  - \"src/ui/**\"\n  - \"project.godot\"\n---\n\n# UI rules\n");
    }

    private string Rules => Path.Combine(_root, ".claude", "rules");

    private string Rule => Path.Combine(Rules, "ui.md");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_RuleFilesMatchExistingPaths()
    {
        var problems = RuleFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithEveryRuleKept_HasNoProblemsAndSkipsLocalAndDotFiles()
    {
        File.WriteAllText(Path.Combine(Rules, "local_device.md"), "No frontmatter.\n");
        File.WriteAllText(Path.Combine(Rules, ".DS_Store"), "");

        var problems = RuleFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("# UI rules\n", "starts with frontmatter that has only paths:")]
    [InlineData("---\npaths: [\"src/ui/**\"]\n---\n", "starts with frontmatter that has only paths:")]
    [InlineData("---\npaths:\n---\n", "starts with frontmatter that has only paths:")]
    [InlineData("---\npaths:\n  - \"src/gui/**\"\n---\n", "src/gui/** is not an existing")]
    [InlineData("---\npaths:\n  - \"Project.cfg\"\n---\n", "Project.cfg is not an existing")]
    [InlineData("---\npaths:\n  - src/ui/**\n---\n", "lists each path on its own line")]
    public void Fixture_WithABrokenRule_NamesTheFileAndRule(string text, string expected)
    {
        File.WriteAllText(Rule, text);

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/rules/ui.md: {expected}");
    }

    [Fact]
    public void Fixture_WithAStrayFile_NamesIt()
    {
        File.WriteAllText(Path.Combine(Rules, "notes.txt"), "Notes.\n");

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/rules/notes.txt: holds only rule files");
    }

    [Fact]
    public void Fixture_WithoutTheRulesFolder_NamesIt()
    {
        Directory.Delete(Rules, recursive: true);

        var problems = RuleFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/rules: missing");
    }
}
