using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// .claude/settings.json registers the repository's hooks but the reviewers' own, with the
/// quoted command path Claude Code needs, and guard-bash.sh checks PR titles as CI's
/// `PR title` check does. See docs/REVIEW.md → "Hooks".
/// </summary>
public sealed partial class HookSettingsSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _ciHeaderPattern = @"^(\w+)(?:\((#[1-9][0-9]*(?:, #[1-9][0-9]*)*)\))?: (.+)$";
    private const string _ciSubjectPattern = "^[A-Z].+$";
    private const string _command = "\"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}\"/.claude/hooks/<name>.sh";
    private const string _changed = "CI's PR title rule changed: update the title check in .claude/hooks/guard-bash.sh to match.";

    private static readonly string _root = RepositoryRoot.Find();
    private static readonly string _hooks = Path.Combine(_root, ".claude", "hooks");

    [Fact]
    public void Settings_RunEachRepositoryHookOnItsEventByItsQuotedPath()
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, ".claude", "settings.json")))!;
        var registered = new List<string>();

        foreach (var (hookEvent, groups) in settings["hooks"]!.AsObject())
        {
            foreach (var group in groups!.AsArray())
            {
                foreach (var hook in group!["hooks"]!.AsArray())
                {
                    hook!["type"]!.GetValue<string>().ShouldBe("command");
                    var match = CommandPath().Match(hook["command"]!.GetValue<string>());
                    match.Success.ShouldBeTrue($"Write the command as {_command}: {hook["command"]}");
                    registered.Add($"{hookEvent} {group["matcher"]?.GetValue<string>() ?? "*"} {match.Groups[1].Value}");
                }
            }
        }

        registered.Order(StringComparer.Ordinal).ShouldBe(
        [
            "PostToolUse Bash after-push.sh",
            "PreToolUse Bash guard-bash.sh",
            "PreToolUse Write|Edit|MultiEdit|NotebookEdit guard-edit.sh",
            "SessionEnd * session-end.sh",
            "SessionStart * session-start.sh",
        ]);
        registered.Select(entry => entry[(entry.LastIndexOf(' ') + 1)..]).Order(StringComparer.Ordinal)
            .ShouldBe(HookScripts()
                .Where(script => script is not (AgentFiles.ReviewerHookScript or AgentFiles.ReviewerLspHookScript))
                .Order(StringComparer.Ordinal));
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var script in HookScripts())
        {
            File.GetUnixFileMode(Path.Combine(_hooks, script)).HasFlag(UnixFileMode.UserExecute)
                .ShouldBeTrue($"{script} is not executable; run git update-index --chmod=+x.");
        }
    }

    /// <summary>
    /// A failing PreToolUse hook denies every matching call, so without jq each hook exits 0 and
    /// allows; session-start.sh says the hooks are off.
    /// </summary>
    [Fact]
    public void EveryHook_WithoutJq_ExitsZeroAndAllows()
    {
        const string payload = "{\"session_id\":\"s\",\"tool_input\":{\"command\":\"git stash\",\"file_path\":\"/tmp/x\"}}";

        foreach (var script in HookScripts())
        {
            var output = fixture.RunRaw(script, payload, path: "/nonexistent");

            output.ShouldBe(script == "session-start.sh" ? "jq is not installed, so the repository hooks are off (README.md → \"Tools\").\n" : "", script);
        }
    }

    [Fact]
    public void CiTitleCheck_UsesTheRuleTheHookImplements()
    {
        var ci = CiTitleCheck();

        ci.Header.ShouldBe(_ciHeaderPattern, _changed);
        ci.Subject.ShouldBe(_ciSubjectPattern, _changed);
        ci.RequireScope.ShouldBe("true", _changed);
        ci.Scopes.ShouldBe(["#[1-9][0-9]*"], _changed);
        ci.Types.Order(StringComparer.Ordinal).ShouldBe(HookTypes().Order(StringComparer.Ordinal), _changed);
    }

    [Theory]
    [InlineData("chore(#1320): Add hooks")]
    [InlineData("refactor(#497, #498): Split the solver")]
    [InlineData("fix(#1): A")]
    [InlineData("fix(#1): a lowercase subject")]
    [InlineData("fix: No scope")]
    [InlineData("fix(#0): Zero issue")]
    [InlineData("wip(#1320): Unknown type")]
    [InlineData("fix(#1,#2): No space")]
    public void Title_GetsTheSameVerdictFromTheHookAndCi(string title)
    {
        var ci = CiTitleCheck();
        var header = Regex.Match(title, ci.Header);
        var ciAccepts = header.Success && ci.Types.Contains(header.Groups[1].Value)
            && header.Groups[2].Success && Regex.IsMatch(header.Groups[3].Value, ci.Subject);
        var output = fixture.Run("guard-bash.sh", HookFixture.Bash($"gh pr create --title '{title}'"));

        (HookFixture.DenyReason(output) is null).ShouldBe(ciAccepts, title);
    }

    private static (string Header, string Subject, string RequireScope, List<string> Scopes, List<string> Types) CiTitleCheck()
    {
        var lines = File.ReadAllLines(Path.Combine(_root, ".github", "workflows", "pr-title.yml"));
        string Value(string key) => lines.Select(line => line.Trim())
            .Single(line => line.StartsWith(key + ": ", StringComparison.Ordinal))[(key.Length + 2)..].Trim('\'');
        List<string> Block(string key)
        {
            var start = Array.FindIndex(lines, line => line.Trim() == key + ": |");
            var indent = lines[start].Length - lines[start].TrimStart().Length;
            return lines.Skip(start + 1)
                .TakeWhile(line => line.Length - line.TrimStart().Length > indent && line.Trim().Length > 0)
                .Select(line => line.Trim()).ToList();
        }

        return (Value("headerPattern"), Value("subjectPattern"), Value("requireScope"), Block("scopes"), Block("types"));
    }

    /// <summary>The repository's hook scripts: every .sh in .claude/hooks but common.sh and local_*.</summary>
    private static IEnumerable<string> HookScripts() =>
        Directory.GetFiles(_hooks, "*.sh").Select(path => Path.GetFileName(path))
            .Where(name => name != "common.sh" && !name.StartsWith(AgentFiles.LocalPrefix, StringComparison.Ordinal));

    private static string[] HookTypes()
    {
        var line = File.ReadAllLines(Path.Combine(_hooks, "guard-bash.sh"))
            .Single(line => line.StartsWith("title_types='", StringComparison.Ordinal));
        return line["title_types='".Length..^1].Split(' ');
    }

    [GeneratedRegex(@"^""\$\{CLAUDE_PROJECT_DIR:-\$\(git rev-parse --show-toplevel\)\}""/\.claude/hooks/([a-z-]+\.sh)$")]
    private static partial Regex CommandPath();
}
