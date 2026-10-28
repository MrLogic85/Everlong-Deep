using System.Text.Json;

namespace EverlongDeep.Cli.Tests;

public sealed class TraceJsonTests : IDisposable
{
    private static readonly string[] _run = ["--seed", "42", "--people", "8", "--hours", "6"];

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("everlong-trace-json-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Run_TraceJson_WritesEachTellingInFull_AndOtherwisePrintsTheSame()
    {
        string path = Path.Combine(_directory.FullName, "trace.json");

        (int exitCode, string output, string error) = Run([.. _run, "--trace-json", path]);

        exitCode.ShouldBe(0);
        error.ShouldBeEmpty();
        output.ShouldBe(Run(_run).Output);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = json.RootElement;
        root.GetProperty("version").GetInt32().ShouldBe(TraceJson.Version);
        root.GetProperty("seed").GetInt32().ShouldBe(42);
        string[] log = [.. root.GetProperty("log").EnumerateArray().Select(line => line.GetString()!)];
        JsonElement[] conversations = [.. root.GetProperty("conversations").EnumerateArray()];
        conversations.Length.ShouldBe(log.Count(line => line.Contains(" tells ")));
        conversations.Select(conversation => conversation.GetProperty("outcome").GetString()).Distinct().Count().ShouldBeGreaterThan(1);
        string[] stories = [.. root.GetProperty("stories").EnumerateArray().Select(story => story.GetProperty("key").GetString()!)];
        root.GetProperty("things").GetArrayLength().ShouldBeGreaterThan(0);
        JsonElement[] people = [.. root.GetProperty("people").EnumerateArray()];
        people.Length.ShouldBe(8);
        people.ShouldAllBe(person => person.GetProperty("traits").EnumerateObject().Count() == Enum.GetValues<Simulation.People.TraitAxis>().Length);
        people.ShouldContain(person => person.GetProperty("labels").GetArrayLength() > 0);

        // Each conversation points at its own telling line, which ends the way its outcome says.
        foreach (JsonElement conversation in conversations)
        {
            string line = log[conversation.GetProperty("logLine").GetInt32()];
            line.ShouldContain(" tells ");
            string expected = line switch
            {
                _ when line.EndsWith(" already holds it") => "alreadyHeld",
                _ when line.Contains(" doesn't believe it (") => "disbelieved",
                _ => "believed",
            };
            conversation.GetProperty("outcome").GetString().ShouldBe(expected, line);
            conversation.GetProperty("believe").ValueKind.ShouldBe(expected == "alreadyHeld" ? JsonValueKind.Null : JsonValueKind.Object);
            string story = conversation.GetProperty("story").GetString()!;
            stories.ShouldContain(story);
            conversation.GetProperty("told").GetProperty("story").GetString().ShouldBe(story);
            conversation.GetProperty("told").GetProperty("text").GetString().ShouldNotBeNullOrEmpty();
            conversation.GetProperty("topics").EnumerateArray().Count(topic => topic.GetProperty("picked").GetBoolean()).ShouldBe(1);
            foreach (string moment in new[] { "before", "after" })
            {
                foreach (string side in new[] { "teller", "listener" })
                {
                    JsonElement snapshot = conversation.GetProperty(moment).GetProperty(side);
                    snapshot.GetProperty("relations").ValueKind.ShouldBe(JsonValueKind.Array);
                    snapshot.GetProperty("beliefs").ValueKind.ShouldBe(JsonValueKind.Array);
                }
            }

            // A listener who believed it holds one more belief, of this story.
            JsonElement before = conversation.GetProperty("before").GetProperty("listener").GetProperty("beliefs");
            JsonElement after = conversation.GetProperty("after").GetProperty("listener").GetProperty("beliefs");
            after.GetArrayLength().ShouldBe(before.GetArrayLength() + (expected == "believed" ? 1 : 0));
            if (expected == "believed")
            {
                JsonElement formed = after[after.GetArrayLength() - 1];
                formed.GetProperty("story").GetString().ShouldBe(story);
                formed.GetProperty("text").GetString().ShouldBe(conversation.GetProperty("told").GetProperty("text").GetString());
            }
        }
    }

    [Fact]
    public void Run_TraceJson_SameSeed_WritesTheSameBytes()
    {
        string first = Path.Combine(_directory.FullName, "first.json");
        string second = Path.Combine(_directory.FullName, "second.json");

        Run([.. _run, "--trace-json", first]);
        Run([.. _run, "--trace-json", second]);

        File.ReadAllBytes(second).ShouldBe(File.ReadAllBytes(first));
    }

    [Fact]
    public void Run_TraceJson_WithoutSeed_WritesTheSeedUsed()
    {
        string path = Path.Combine(_directory.FullName, "trace.json");

        string output = Run(["--hours", "0", "--trace-json", path]).Output;

        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(path));
        output.Split('\n')[0].Trim().ShouldBe($"Seed: {json.RootElement.GetProperty("seed").GetInt32()}");
    }

    [Theory]
    [InlineData("missing/trace.json")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("bad\0name.json")]
    public void Run_TraceJson_UnwritablePath_PrintsUsageAndFailsBeforeTheRun(string relative)
    {
        string path = relative.Length == 0 ? "" : Path.Combine(_directory.FullName, relative);

        (int exitCode, string output, string error) = Run([.. _run, "--trace-json", path]);

        exitCode.ShouldNotBe(0);
        output.ShouldBeEmpty();
        error.ShouldStartWith("Usage:");
        _directory.EnumerateFileSystemInfos().ShouldBeEmpty();
    }

    [Fact]
    public void Run_TraceJsonTwice_PrintsUsageAndFails()
    {
        string path = Path.Combine(_directory.FullName, "trace.json");

        Run([.. _run, "--trace-json", path, "--trace-json", path]).ExitCode.ShouldNotBe(0);

        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void Run_TraceJson_FolderNotWritable_ReportsItAfterTheRun_AndLeavesNoFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        DirectoryInfo locked = _directory.CreateSubdirectory("locked");
        string path = Path.Combine(locked.FullName, "trace.json");
        locked.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserExecute;
        try
        {
            (int exitCode, string output, string error) = Run([.. _run, "--trace-json", path]);

            exitCode.ShouldNotBe(0);
            output.ShouldBe(Run(_run).Output);
            error.ShouldStartWith($"Couldn't write {path}");
            locked.EnumerateFileSystemInfos().ShouldBeEmpty();
        }
        finally
        {
            locked.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        }
    }

    private static (int ExitCode, string Output, string Error) Run(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = Program.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
