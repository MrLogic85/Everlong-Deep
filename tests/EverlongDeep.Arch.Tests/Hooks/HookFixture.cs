using System.Diagnostics;
using System.Text.Json.Nodes;

namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// Runs a repository hook from .claude/hooks against a throwaway git repository, the way Claude
/// Code does: the payload as JSON on stdin and CLAUDE_PROJECT_DIR set to the project.
/// The repository is the fixture's temp directory, made under the test output rather than
/// /tmp, since guard-edit.sh denies writes into /tmp. Specs that only read the repository share
/// one through IClassFixture. Git runs without the user's or system's config, so a global
/// setting such as commit.gpgsign cannot change the result.
/// </summary>
public sealed class HookFixture : IDisposable
{
    internal const string SessionId = "fixture-session";

    private static readonly string _hooks = Path.Combine(RepositoryRoot.Find(), ".claude", "hooks");

    public HookFixture()
        : this("main")
    {
    }

    internal HookFixture(string branch)
    {
        HasJq().ShouldBeTrue("The hook specs need jq on PATH (README.md → \"Tools\").");
        Root = Path.Combine(AppContext.BaseDirectory, "hook-fixtures", Guid.NewGuid().ToString("N"));
        InitRepository(Root, branch);
    }

    internal string Root { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    internal string Checkout(string name, string branch)
    {
        var directory = Path.Combine(Root, name);
        if (!Directory.Exists(directory))
        {
            InitRepository(directory, branch);
        }

        return directory;
    }

    private static void InitRepository(string directory, string branch)
    {
        Directory.CreateDirectory(directory);
        Git(directory, "init", "-q", "-b", "main");
        Git(directory, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com",
            "commit", "-q", "--allow-empty", "-m", "Start");
        if (branch != "main")
        {
            Git(directory, "switch", "-q", "-c", branch);
        }
    }

    /// <summary>Runs the hook, when given only that PATH, requires exit code 0, and returns its stdout.</summary>
    internal string Run(string hook, JsonObject payload, string? cwd = null, string? path = null)
    {
        payload["session_id"] = SessionId;
        payload["cwd"] = cwd ?? Root;
        return RunRaw(hook, payload.ToJsonString(), path);
    }

    /// <summary>
    /// Runs the hook through bash with raw stdin, when given only that PATH, and any extra
    /// environment variables; requires exit code 0 and returns its stdout.
    /// </summary>
    internal string RunRaw(string hook, string stdin, string? path = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(_hooks, hook));
        Isolate(start);
        start.Environment["CLAUDE_PROJECT_DIR"] = Root;
        if (path is not null)
        {
            start.Environment["PATH"] = path;
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        try
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The hook may exit without reading its input, for example when jq is missing.
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit(10_000).ShouldBeTrue($"{hook} timed out.");
        process.ExitCode.ShouldBe(0, $"{hook} failed: {errors.Result}");
        return output.Result;
    }

    /// <summary>
    /// The PreToolUse deny reason, or null when the hook allows the call. A deny carries only
    /// Claude Code's hookSpecificOutput.
    /// </summary>
    internal static string? DenyReason(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var json = JsonNode.Parse(output)!;
        json.AsObject().Select(entry => entry.Key).ShouldBe(["hookSpecificOutput"]);
        var specific = json["hookSpecificOutput"]!;
        specific["hookEventName"]!.GetValue<string>().ShouldBe("PreToolUse");
        specific["permissionDecision"]!.GetValue<string>().ShouldBe("deny");
        return specific["permissionDecisionReason"]!.GetValue<string>();
    }

    /// <summary>Asserts the hook allowed the call when reason is null, or denied it for that reason.</summary>
    internal static void ShouldDecide(string output, string? reason)
    {
        if (reason is null)
        {
            DenyReason(output).ShouldBeNull();
        }
        else
        {
            DenyReason(output).ShouldNotBeNull().ShouldStartWith(reason);
        }
    }

    internal static JsonObject Bash(string command) =>
        new() { ["tool_name"] = "Bash", ["tool_input"] = new JsonObject { ["command"] = command } };

    /// <summary>Runs a repository script with bash in <paramref name="directory"/>; returns its exit code and output.</summary>
    internal static (int ExitCode, string Output) Script(string directory, string script, params string[] arguments)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Isolate(start);
        start.ArgumentList.Add(Path.Combine(RepositoryRoot.Find(), script));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit(10_000).ShouldBeTrue($"{script} timed out.");
        return (process.ExitCode, output.Result + errors.Result);
    }

    internal static string Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
        Isolate(start);
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(directory);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)}: {errors.Result}");
        return output.Result;
    }

    private static bool HasJq()
    {
        try
        {
            using var jq = Process.Start(new ProcessStartInfo("jq", "--version") { RedirectStandardOutput = true })!;
            jq.WaitForExit();
            return jq.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void Isolate(ProcessStartInfo start)
    {
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        foreach (var name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_CONFIG_PARAMETERS" })
        {
            start.Environment.Remove(name);
        }
    }
}
