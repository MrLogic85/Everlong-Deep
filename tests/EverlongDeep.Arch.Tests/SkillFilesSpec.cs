namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Skills load in Claude Code: docs/REVIEW.md → "Agent, rule and skill files".
/// The fixtures live in a temp directory, so Claude Code does not load them as skills.
/// </summary>
public sealed class SkillFilesSpec : IDisposable
{
    private const string _name = "fixture-skill";

    // The local_ in the folder name proves only paths inside the repository count as local.
    private readonly string _root = Directory.CreateTempSubdirectory("local_skill-files-").FullName;

    public SkillFilesSpec()
    {
        WriteSkill(_name, $"---\n# A comment line.\nname: {_name}\ndescription: \"Does fixtures: use # when asked.\"\n---\n\n# Fixture\n");
    }

    private string Skills => Path.Combine(_root, ".claude", "skills");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Repository_SkillFilesFollowTheRules()
    {
        var problems = SkillFiles.Problems(RepositoryRoot.Find());

        problems.ShouldBeEmpty("See docs/REVIEW.md → \"Agent, rule and skill files\".");
    }

    [Fact]
    public void Fixture_WithEveryRuleKept_HasNoProblemsAndSkipsLocalAndDotFiles()
    {
        WriteSkill("local_device", "No frontmatter.\n");
        File.WriteAllText(Path.Combine(Skills, ".DS_Store"), "");
        File.WriteAllText(Path.Combine(_root, "AGENTS.md"), $"Use the `{_name}` skill.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("# Fixture\n", "no frontmatter")]
    [InlineData("---\nname: other\ndescription: Does fixtures.\n---\n", $"name must be {_name}")]
    [InlineData($"---\nname: {_name}\n---\n", "needs a one-line description")]
    [InlineData($"---\nname: {_name}\ndescription: {{long}}\n---\n", "description is over 1024 characters")]
    [InlineData($"---\nname: {_name}\ndescription: >\n  Does fixtures.\n---\n", "description must be one line")]
    [InlineData($"---\nname: {_name}\ndescription: Does fixtures: when asked.\n---\n", "description has an unquoted")]
    [InlineData($"---\nname: {_name}\ndescription: Does fixtures.\nallowed-tools: Bash\n---\n", "sets allowed-tools")]
    [InlineData($"---\nname: {_name}\nname: {_name}\ndescription: Does fixtures.\n---\n", "sets name twice")]
    public void Fixture_WithABrokenSkill_NamesTheFileAndRule(string text, string expected)
    {
        WriteSkill(_name, text.Replace("{long}", new string('a', 1025), StringComparison.Ordinal));

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith($".claude/skills/{_name}/SKILL.md: {expected}");
    }

    [Fact]
    public void Fixture_WithEntriesThatAreNoSkills_NamesEach()
    {
        var tooLong = new string('a', 65);
        foreach (var folder in new[] { "Fixture_Skill", tooLong })
        {
            WriteSkill(folder, $"---\nname: {folder}\ndescription: Does fixtures.\n---\n");
        }

        Directory.CreateDirectory(Path.Combine(Skills, "empty"));
        File.WriteAllText(Path.Combine(Skills, "notes.md"), "Notes.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldBe(
        [
            ".claude/skills/Fixture_Skill: name the folder in lowercase letters, digits and hyphens, at most 64.",
            $".claude/skills/{tooLong}: name the folder in lowercase letters, digits and hyphens, at most 64.",
            ".claude/skills/empty/SKILL.md: missing.",
            ".claude/skills/notes.md: holds only skill folders, each with a SKILL.md.",
        ]);
    }

    [Fact]
    public void Fixture_WhenInstructionsNameAMissingSkill_NamesEachFile()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
        Directory.CreateDirectory(Path.Combine(_root, ".claude", "agents"));
        File.WriteAllText(Path.Combine(_root, "docs", "FLOW.md"), "Run the `ship-it`\nskill.\n");
        File.WriteAllText(Path.Combine(_root, ".claude", "agents", "fixture-lead.md"), "Run the `ship-it` skill.\n");

        var problems = SkillFiles.Problems(_root);

        problems.ShouldBe(
        [
            ".claude/agents/fixture-lead.md: names the ship-it skill, which has no .claude/skills/ship-it/SKILL.md.",
            "docs/FLOW.md: names the ship-it skill, which has no .claude/skills/ship-it/SKILL.md.",
        ]);
    }

    [Fact]
    public void Fixture_WithoutTheSkillsFolder_NamesIt()
    {
        Directory.Delete(Skills, recursive: true);

        var problems = SkillFiles.Problems(_root);

        problems.ShouldHaveSingleItem().ShouldStartWith(".claude/skills: missing");
    }

    private void WriteSkill(string folder, string text)
    {
        Directory.CreateDirectory(Path.Combine(Skills, folder));
        File.WriteAllText(Path.Combine(Skills, folder, "SKILL.md"), text);
    }
}
