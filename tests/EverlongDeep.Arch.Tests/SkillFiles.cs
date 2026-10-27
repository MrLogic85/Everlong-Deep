using System.Text.RegularExpressions;

namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Checks the skill files in .claude/skills: docs/REVIEW.md → "Agent, rule and skill files".
/// A skill whose name or description is off loads in only one tool, or is never matched to a task.
/// </summary>
internal static partial class SkillFiles
{
    private const string _skillFile = "SKILL.md";

    // Claude Code's limit on a description; a longer one is cut where the model matches tasks.
    private const int _maxDescription = 1024;

    // Claude Code's limit on a skill name.
    private const int _maxName = 64;

    private static readonly string[] _allowedKeys = ["name", "description"];

    // Where instructions name skills, as "the `<name>` skill".
    private static readonly string[] _citingFolders = ["docs", ".claude/rules", ".claude/skills", ".claude/agents"];

    /// <summary>Every broken skill under <paramref name="root"/>, each naming its file.</summary>
    internal static List<string> Problems(string root)
    {
        var problems = new List<string>();
        var skills = Path.Combine(root, ".claude", "skills");
        if (!Directory.Exists(skills))
        {
            return [".claude/skills: missing; the skills live there."];
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(skills).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (AgentFiles.IsSkipped(name))
            {
                continue;
            }

            var shown = $".claude/skills/{name}";
            if (!Directory.Exists(path))
            {
                problems.Add($"{shown}: holds only skill folders, each with a {_skillFile}.");
                continue;
            }

            if (!SkillName().IsMatch(name) || name.Length > _maxName)
            {
                problems.Add($"{shown}: name the folder in lowercase letters, digits and hyphens, at most {_maxName}.");
            }

            var file = Path.Combine(path, _skillFile);
            if (!File.Exists(file))
            {
                problems.Add($"{shown}/{_skillFile}: missing.");
                continue;
            }

            CheckFrontmatter($"{shown}/{_skillFile}", name, File.ReadAllLines(file), problems);
        }

        CheckCitations(root, skills, problems);
        return problems;
    }

    /// <summary>A skill the instructions name but that does not exist is a step no tool can load.</summary>
    private static void CheckCitations(string root, string skills, List<string> problems)
    {
        var files = AgentFiles.Instructions(root, _citingFolders)
            .Prepend(Path.Combine(root, "AGENTS.md"))
            .Prepend(Path.Combine(root, "README.md"))
            .Where(File.Exists)
            .Order(StringComparer.Ordinal);
        foreach (var path in files)
        {
            var cited = CitedSkill().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value);
            foreach (var name in cited.Distinct().Where(name => !File.Exists(Path.Combine(skills, name, _skillFile))))
            {
                problems.Add($"{Path.GetRelativePath(root, path).Replace('\\', '/')}: names the {name} skill, which has no .claude/skills/{name}/{_skillFile}.");
            }
        }
    }

    private static void CheckFrontmatter(string shown, string name, string[] lines, List<string> problems)
    {
        var end = AgentFiles.FrontmatterEnd(lines);
        if (end < 0)
        {
            problems.Add($"{shown}: no frontmatter between two --- lines.");
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        foreach (var line in lines[1..end].Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#')))
        {
            if (line[0] is ' ' or '-')
            {
                problems.Add($"{shown}: {current ?? "the frontmatter"} must be one line.");
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            current = colon > 0 ? line[..colon] : line;
            var value = colon > 0 ? line[(colon + 1)..].Trim() : "";
            if (!values.TryAdd(current, value))
            {
                problems.Add($"{shown}: sets {current} twice.");
            }

            AgentFiles.CheckScalar(shown, current, value, problems);
        }

        foreach (var key in values.Keys.Where(key => !_allowedKeys.Contains(key)))
        {
            problems.Add($"{shown}: sets {key}; skill frontmatter has only {string.Join(", ", _allowedKeys)}.");
        }

        if (!values.TryGetValue("name", out var actual) || actual != name)
        {
            problems.Add($"{shown}: name must be {name}, the folder's name.");
        }

        if (!values.TryGetValue("description", out var description) || description.Length == 0)
        {
            problems.Add($"{shown}: needs a one-line description of what it does and when to use it.");
        }
        else if (description.Length > _maxDescription)
        {
            problems.Add($"{shown}: description is over {_maxDescription} characters.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SkillName();

    [GeneratedRegex(@"`([a-z0-9]+(?:-[a-z0-9]+)*)`\s+skill\b")]
    private static partial Regex CitedSkill();
}
