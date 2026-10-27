using System.Text.Json.Nodes;

namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-edit.sh denies edit-tool writes into /tmp.
/// </summary>
public sealed class GuardEditHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-edit.sh";
    private const string _tmp = "Do not write scratch files to /tmp";

    private readonly HookFixture _fixture = fixture;

    [Theory]
    [InlineData("file_path", "/tmp/notes.md", _tmp)]
    [InlineData("file_path", "/tmp", _tmp)]
    [InlineData("file_path", "/private/tmp/shot.png", _tmp)]
    [InlineData("file_path", "/var/../tmp/notes.md", _tmp)]
    [InlineData("file_path", ".scratch/shot.png", null)]
    [InlineData("file_path", "/tmpfiles/notes.md", null)]
    [InlineData("notebook_path", "/tmp/notes.ipynb", _tmp)]
    public void Edit_IsDeniedOnlyIntoTmp(string field, string path, string? reason) =>
        HookFixture.ShouldDecide(_fixture.Run(_hook, Edit(field, path)), reason);

    private JsonObject Edit(string field, string path)
    {
        path = path.Replace("{root}", _fixture.Root, StringComparison.Ordinal);
        return new()
        {
            ["tool_name"] = "Edit",
            ["tool_input"] = new JsonObject { [field] = path },
        };
    }
}
