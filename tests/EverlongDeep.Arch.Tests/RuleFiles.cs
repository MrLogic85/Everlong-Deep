namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Checks the rule files in .claude/rules: docs/REVIEW.md → "Agent, rule and skill files". A rule
/// whose glob matches nothing never loads, and nothing else notices when its folder moves.
/// </summary>
internal static class RuleFiles
{
    private const string _anything = "/**";

    /// <summary>Every broken rule file under <paramref name="root"/>, each naming its file.</summary>
    internal static List<string> Problems(string root)
    {
        var problems = new List<string>();
        var rules = Path.Combine(root, ".claude", "rules");
        if (!Directory.Exists(rules))
        {
            return [".claude/rules: missing; the folder and layer rules live there."];
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(rules).Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileName(path);
            if (AgentFiles.IsSkipped(file))
            {
                continue;
            }

            var shown = $".claude/rules/{file}";
            if (!file.EndsWith(".md", StringComparison.Ordinal) || Directory.Exists(path))
            {
                problems.Add($"{shown}: holds only rule files ending in .md.");
                continue;
            }

            CheckPaths(root, shown, File.ReadAllLines(path), problems);
        }

        return problems;
    }

    private static void CheckPaths(string root, string shown, string[] lines, List<string> problems)
    {
        var end = AgentFiles.FrontmatterEnd(lines);
        if (end < 3 || lines[1] != "paths:")
        {
            problems.Add($"{shown}: starts with frontmatter that has only paths: with at least one glob.");
            return;
        }

        foreach (var line in lines[2..end])
        {
            if (!line.StartsWith("  - \"", StringComparison.Ordinal) || !line.EndsWith('"'))
            {
                problems.Add($"{shown}: lists each path on its own line as - \"<glob>\", not {line.Trim()}.");
                continue;
            }

            var glob = line[5..^1];
            var folder = glob.EndsWith(_anything, StringComparison.Ordinal) ? glob[..^_anything.Length] : null;
            var exists = folder is null
                ? !glob.Contains('*', StringComparison.Ordinal) && File.Exists(Path.Combine(root, glob))
                : !folder.Contains('*', StringComparison.Ordinal) && Directory.Exists(Path.Combine(root, folder));
            if (!exists)
            {
                problems.Add($"{shown}: {glob} is not an existing file or <folder>{_anything}.");
            }
        }
    }
}
