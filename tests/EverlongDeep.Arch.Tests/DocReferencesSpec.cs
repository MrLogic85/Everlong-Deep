namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Agent files name the doc heading they mean: docs/REVIEW.md → "Agent, rule and skill files". The
/// fixtures live in a temp directory, so Claude Code does not load them as skills.
/// </summary>
public sealed class DocReferencesSpec : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("doc-references-").FullName;

    public DocReferencesSpec()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
        Directory.CreateDirectory(Path.GetDirectoryName(Skill)!);
        File.WriteAllText(Path.Combine(_root, "docs", "RULES.md"), "# Rules\n\n## Status\n\n## 9. Comments and docs\n\n### Parent issues\n\n```\n## Size\n```\n");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# Readme\n\n## Tools\n");
    }

    private string Skill => Path.Combine(_root, ".claude", "skills", "fixture", "SKILL.md");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_DocReferencesNameExistingHeadings()
    {
        var problems = DocReferences.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithEveryReferenceNamingAHeading_HasNoProblems()
    {
        File.WriteAllText(Skill, """
            Read `docs/RULES.md` → "Status", `docs/RULES.md` →
              "Status" and "Parent issues", and `docs/RULES.md` §9's questions.
            `docs/RULES.md` owns the rules. See `README.md` → "Tools", write `docs/X.md` → "Heading", and read `LOCAL_CONFIG.md`.
            """);
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "agents"));
        File.WriteAllText(Path.Combine(_root, ".claude", "agents", "local_device.md"), "Read `docs/RULES.md`.\n");

        var problems = DocReferences.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Read `docs/RULES.md` first.", "`docs/RULES.md` names no heading; write")]
    [InlineData("`docs/RULES.md` owned the rules.", "`docs/RULES.md` names no heading; write")]
    [InlineData("Read `docs/RULES.md` → \"Statuses\".", "`docs/RULES.md` → \"Statuses\" names \"Statuses\", which is no heading")]
    [InlineData("Read `docs/RULES.md` → \"Status\" and \"Size\".", "`docs/RULES.md` → \"Status\" and \"Size\" names \"Size\", which is no heading")]
    [InlineData("Read `docs/RULES.md` §4.", "`docs/RULES.md` §4 names §4, which is no \"4. …\" heading")]
    [InlineData("Read `docs/RULES.md` §9.2.", "`docs/RULES.md` names no heading; write")]
    [InlineData("Read `docs/RULES.md` §91.2.", "`docs/RULES.md` names no heading; write")]
    [InlineData("Read `docs/GONE.md` → \"Status\".", "`docs/GONE.md` → \"Status\" names a doc that does not exist")]
    public void Fixture_WithABrokenReference_NamesTheFileAndReference(string text, string expected)
    {
        File.WriteAllText(Skill, text);

        var problems = DocReferences.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/skills/fixture/SKILL.md: {expected}");
    }
}
