using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// session-start.sh tells the agent the branch and uncommitted changes; after-push.sh reports,
/// once after a git push, the emulators and APKs a session left behind, compared with its start.
/// The hooks run with a stub <c>ps</c> that lists only the processes this test started, so
/// another test run's emulators, real or fake, never look like this session's.
/// </summary>
public sealed class SessionHooksSpec : IDisposable
{
    private readonly HookFixture _fixture = new("feature/1320-hooks");
    private readonly List<Process> _emulators = [];

    public SessionHooksSpec() => StubPs();

    private string Build => Path.Combine(_fixture.Root, "build");

    private string State => Path.Combine(_fixture.Root, ".git", "agent-hooks", HookFixture.SessionId);

    public void Dispose()
    {
        foreach (var emulator in _emulators)
        {
            if (!emulator.HasExited)
            {
                emulator.Kill();
            }

            emulator.Dispose();
        }

        _fixture.Dispose();
    }

    [Fact]
    public void SessionStart_WithACleanCheckoutAndNoToolManifest_NamesTheBranchWithoutRestoring()
    {
        var log = StubDotnet("exit 1");
        File.Delete(Path.Combine(_fixture.Root, "dotnet-tools.json"));

        var context = StartSession();

        context.ShouldBe("Working copy at session start: branch feature/1320-hooks, no uncommitted changes.");
        File.Exists(log).ShouldBeFalse();
    }

    [Fact]
    public void SessionStart_WithUncommittedChanges_ListsThemSaysToAskAndRestoresTheToolsFromTheRoot()
    {
        var log = StubDotnet("exit 0");
        File.WriteAllText(Path.Combine(_fixture.Root, "notes.md"), "Notes.\n");

        var context = StartSession();

        context.ShouldContain("2 uncommitted changes:\n?? dotnet-tools.json\n?? notes.md\n");
        context.ShouldContain("stop and ask the human");
        context.ShouldNotContain("dotnet tool restore");
        File.ReadAllText(log).ShouldBe($"{_fixture.Root} tool restore\n");
    }

    [Fact]
    public void SessionStart_WithManyChanges_ListsTenAndCountsTheRest()
    {
        for (var i = 0; i < 12; i++)
        {
            File.WriteAllText(Path.Combine(_fixture.Root, $"note{i:00}.md"), "");
        }

        var context = StartSession();

        context.ShouldContain("12 uncommitted changes:\n?? note00.md\n");
        context.ShouldContain("?? note09.md\n… and 2 more.\n");
        context.ShouldNotContain("note10");
    }

    [Fact]
    public void SessionStart_OnResume_ReportsWithoutAskingOrRestoring()
    {
        var log = StubDotnet("exit 1");

        var context = StartSession("resume");

        context.ShouldStartWith("Working copy now: branch feature/1320-hooks, 1 uncommitted changes:");
        context.ShouldNotContain("stop and ask");
        context.ShouldNotContain("dotnet tool restore");
        File.Exists(log).ShouldBeFalse();
    }

    [Fact]
    public void SessionStart_Again_KeepsTheFirstBaseline()
    {
        StartSession();
        var during = StartEmulator();
        File.WriteAllText(Path.Combine(_fixture.Root, "notes.md"), "Notes.\n");

        var context = StartSession("startup");

        context.ShouldNotContain("stop and ask");
        Reminder(Push()).ShouldContain($"emulator pid {during.Id}");
    }

    [Fact]
    public void SessionStart_WhenGitFails_SaysToAsk()
    {
        Directory.Move(Path.Combine(_fixture.Root, ".git"), Path.Combine(_fixture.Root, "moved.git"));
        File.WriteAllText(Path.Combine(_fixture.Root, ".git"), "gitdir: missing.git\n");

        var context = StartSession();

        context.ShouldStartWith("Could not read the working copy");
        context.ShouldContain("Ask the human");
        context.ShouldEndWith("the after-push check for emulators and APKs is off.");
    }

    [Fact]
    public void SessionStart_PrunesOnlyStaleSessions()
    {
        var sessions = Path.GetDirectoryName(State)!;
        var stale = Directory.CreateDirectory(Path.Combine(sessions, "stale")).FullName;
        var live = Directory.CreateDirectory(Path.Combine(sessions, "live")).FullName;
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-49));
        Directory.SetLastWriteTimeUtc(live, DateTime.UtcNow.AddHours(-47));

        StartSession();

        Directory.Exists(stale).ShouldBeFalse();
        Directory.Exists(live).ShouldBeTrue();
    }

    [Fact]
    public void AfterPush_OfAnyCommand_KeepsALongSessionFromLookingStale()
    {
        StartSession();
        Directory.SetLastWriteTimeUtc(State, DateTime.UtcNow.AddDays(-4));

        Push("git status");

        Directory.GetLastWriteTimeUtc(State).ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void WithoutASessionId_TheHooksKeepNoStateAndSaySo()
    {
        StartSession();
        StartEmulator();
        var input = new JsonObject
        {
            ["cwd"] = _fixture.Root,
            ["tool_input"] = new JsonObject { ["command"] = "git push" },
        };

        _fixture.RunRaw("after-push.sh", input.ToJsonString()).ShouldBeEmpty();
        _fixture.RunRaw("session-end.sh", input.ToJsonString()).ShouldBeEmpty();
        var output = _fixture.RunRaw("session-start.sh", new JsonObject { ["cwd"] = _fixture.Root }.ToJsonString());

        Directory.Exists(State).ShouldBeTrue();
        JsonNode.Parse(output)!["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>()
            .ShouldEndWith("the after-push check for emulators and APKs is off.");
    }

    [Fact]
    public void SessionStart_WhenTheToolRestoreFails_SaysToUseGitGrep()
    {
        StubDotnet("exit 1");

        var context = StartSession();

        context.ShouldEndWith("`dotnet tool restore` failed, so the LSP tool cannot start the C# language server; find callers and definitions with `git grep` instead.");
    }

    [Fact]
    public void SessionStart_WhenTheToolRestoreStalls_StopsWaitingAndSaysToUseGitGrep()
    {
        // The hook leaves the stalled restore running, so it sleeps only as long as the bound below.
        StubDotnet("exec sleep 6");
        var clock = Stopwatch.StartNew();

        var context = StartSession(environment: new Dictionary<string, string> { ["EVERLONG_DEEP_RESTORE_WAIT"] = "1" });

        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(6));
        context.ShouldEndWith("`dotnet tool restore` is still running, so the LSP tool may fail to start the C# language server for a while; find callers and definitions with `git grep` until it works.");
    }

    [Fact]
    public void SessionStart_WithoutDotnet_SkipsTheRestoreQuietly()
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "dotnet-tools.json"), "{}\n");

        var context = StartSession(path: PathWithoutDotnet());

        context.ShouldStartWith("Working copy at session start: branch feature/1320-hooks");
        context.ShouldNotContain("dotnet tool restore");
    }

    [Fact]
    public void AfterPush_WithQemuOnlyAsAnArgument_IsSilent()
    {
        StartSession();
        var start = new ProcessStartInfo("bash");
        foreach (var argument in new[] { "-c", "sleep 60; :", "qemu-system-argument" })
        {
            start.ArgumentList.Add(argument);
        }

        Track(Process.Start(start)!);
        Thread.Sleep(100);

        Push().ShouldBeEmpty();
    }

    [Fact]
    public void AfterPush_WithAnEmulatorAndApkFromThisSession_ReportsOnlyThoseOnce()
    {
        Directory.CreateDirectory(Build);
        var old = Path.Combine(Build, "everlong-deep-0.14.2.apk");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        var before = StartEmulator();
        StartSession();
        var during = StartEmulator();
        File.WriteAllText(Path.Combine(Build, "everlong-deep-0.14.3.apk"), "");
        File.SetLastWriteTimeUtc(Path.Combine(Build, "everlong-deep-0.14.3.apk"), DateTime.UtcNow.AddMinutes(1));

        var first = Reminder(Push());

        first.ShouldContain($"emulator pid {during.Id}");
        first.ShouldContain("build/everlong-deep-0.14.3.apk");
        first.ShouldNotContain($"emulator pid {before.Id}");
        first.ShouldNotContain("0.14.2");
        Push().ShouldBeEmpty();
    }

    /// <summary>Only a git push that sends something reminds, read as guard-bash.sh reads the line.</summary>
    [Theory]
    [InlineData("git commit -m \"Remind after git push instead\"; echo legit push", false)]
    [InlineData("git push --dry-run", false)]
    [InlineData("git push -nu origin main", false)]
    [InlineData("GIT_TRACE=0 /usr/bin/git -C sub -c core.sshCommand=ssh push 2>&1 | tail -5", true)]
    [InlineData("{ time git push --force-with-lease; }", true)]
    public void AfterPush_RemindsOnlyAfterAPush(string command, bool reminds)
    {
        StartSession();
        var during = StartEmulator();

        var output = Push(command);

        if (reminds)
        {
            Reminder(output).ShouldContain($"emulator pid {during.Id}");
        }
        else
        {
            output.ShouldBeEmpty();
        }
    }

    [Fact]
    public void SessionEnd_RemovesTheSessionState()
    {
        StartSession();
        Directory.Exists(State).ShouldBeTrue();

        _fixture.Run("session-end.sh", new JsonObject { ["hook_event_name"] = "SessionEnd" });

        Directory.Exists(State).ShouldBeFalse();
    }

    private static string Reminder(string output)
    {
        var json = JsonNode.Parse(output)!;
        json.AsObject().Select(entry => entry.Key).ShouldBe(["hookSpecificOutput"]);
        json["hookSpecificOutput"]!["hookEventName"]!.GetValue<string>().ShouldBe("PostToolUse");
        var context = json["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>();
        context.ShouldStartWith("Hook after-push.sh, a reminder");
        return context;
    }

    private string StubBin => Path.Combine(_fixture.Root, ".git", "stub-bin");

    private string StubPath => $"{StubBin}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";

    private string Started => Path.Combine(StubBin, "started-pids");

    /// <summary>A <c>ps</c> that passes its arguments to the real one and keeps only the rows of the pids in <see cref="Started"/>.</summary>
    private void StubPs() =>
        WriteStub("ps", $"#!/bin/sh\n/bin/ps \"$@\" | awk -v started='{Started}' 'BEGIN {{ while ((getline pid < started) > 0) mine[pid] }} $1 in mine'\n");

    private void WriteStub(string name, string script)
    {
        Directory.CreateDirectory(StubBin);
        var stub = Path.Combine(StubBin, name);
        File.WriteAllText(stub, script);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(stub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private Process Track(Process process)
    {
        _emulators.Add(process);
        File.AppendAllText(Started, $"{process.Id.ToString(CultureInfo.InvariantCulture)}\n");
        return process;
    }

    /// <summary>
    /// Adds a tool manifest and a stub <c>dotnet</c> that logs its folder and arguments, writes
    /// to stdout and stderr as the real one does, then runs <paramref name="end"/>; returns the
    /// log's path.
    /// </summary>
    private string StubDotnet(string end)
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "dotnet-tools.json"), "{}\n");
        var log = Path.Combine(StubBin, "dotnet.log");
        WriteStub("dotnet", $"#!/bin/sh\necho \"$PWD $*\" >'{log}'\necho Restoring\necho warning >&2\n{end}\n");
        return log;
    }

    /// <summary>
    /// A PATH of one folder that links every program on the real PATH except <c>dotnet</c>, and
    /// keeps the stub <c>ps</c>.
    /// </summary>
    private string PathWithoutDotnet()
    {
        var linked = new HashSet<string>(StringComparer.Ordinal) { "dotnet", "ps" };
        foreach (var folder in Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator).Where(Directory.Exists))
        {
            foreach (var program in Directory.EnumerateFiles(folder).Where(program => linked.Add(Path.GetFileName(program))))
            {
                File.CreateSymbolicLink(Path.Combine(StubBin, Path.GetFileName(program)), program);
            }
        }

        return StubBin;
    }

    private string StartSession(string source = "startup", string? path = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var payload = new JsonObject
        {
            ["hook_event_name"] = "SessionStart",
            ["source"] = source,
            ["session_id"] = HookFixture.SessionId,
            ["cwd"] = _fixture.Root,
        };
        var json = JsonNode.Parse(_fixture.RunRaw("session-start.sh", payload.ToJsonString(), path ?? StubPath, environment))!;
        json.AsObject().Select(entry => entry.Key).ShouldBe(["hookSpecificOutput"]);
        json["hookSpecificOutput"]!["hookEventName"]!.GetValue<string>().ShouldBe("SessionStart");
        var context = json["hookSpecificOutput"]!["additionalContext"]!.GetValue<string>();
        return context;
    }

    private string Push(string command = "git push") =>
        _fixture.Run("after-push.sh", new JsonObject
        {
            ["hook_event_name"] = "PostToolUse",
            ["tool_name"] = "Bash",
            ["tool_input"] = new JsonObject { ["command"] = command },
        }, path: StubPath);

    /// <summary>
    /// A sleep whose program name is the emulator's, which is what the hooks look for.
    /// </summary>
    private Process StartEmulator()
    {
        var start = new ProcessStartInfo("bash");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"exec -a qemu-system-fixture{_emulators.Count} sleep 60");
        var process = Track(Process.Start(start)!);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ProgramOf(process).StartsWith("qemu-system", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }

        return process;
    }

    private static string ProgramOf(Process process)
    {
        var start = new ProcessStartInfo("ps") { RedirectStandardOutput = true };
        foreach (var argument in new[] { "-o", "args=", "-p", process.Id.ToString(CultureInfo.InvariantCulture) })
        {
            start.ArgumentList.Add(argument);
        }

        using var ps = Process.Start(start)!;
        var args = ps.StandardOutput.ReadToEnd().Trim();
        ps.WaitForExit();
        return args;
    }
}
