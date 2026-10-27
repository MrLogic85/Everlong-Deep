using System.Text.Json.Nodes;

namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Claude Code's LSP tool starts the pinned C# language server: docs/REVIEW.md → "Agent, rule
/// and skill files". A broken link here only makes the agents fall back to git grep, so nothing
/// else would notice it.
/// </summary>
public sealed class LspConfigSpec
{
    private static readonly string _root = RepositoryRoot.Find();

    [Fact]
    public void Plugin_RunsTheServerForCSharp_PinnedInTheToolManifest()
    {
        var server = Read(".claude", "marketplace", Plugin(), ".lsp.json")["csharp"]!;
        var tools = Read("dotnet-tools.json")["tools"]!.AsObject();

        var command = Command(server);

        server["extensionToLanguage"]![".cs"]!.GetValue<string>().ShouldBe("csharp");
        command.Take(3).ShouldBe(["dotnet", "tool", "run"]);
        var tool = tools.Single(entry => entry.Value!["commands"]!.AsArray().Any(name => name!.GetValue<string>() == command[3]));
        tool.Value!["version"]!.GetValue<string>().ShouldMatch(@"^\d+\.\d+\.\d+$");
        tool.Value!["rollForward"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void ClaudeSettings_EnableThePluginFromTheRepositoryMarketplace()
    {
        var settings = Read(".claude", "settings.json");
        var marketplace = Read(".claude", "marketplace", ".claude-plugin", "marketplace.json");
        var name = marketplace["name"]!.GetValue<string>();

        settings["extraKnownMarketplaces"]![name]!["source"]!.ToJsonString()
            .ShouldBe(new JsonObject { ["source"] = "directory", ["path"] = "./.claude/marketplace" }.ToJsonString());
        settings["enabledPlugins"]!.AsObject().Select(entry => entry.Key).ShouldBe([$"{Plugin()}@{name}"]);
        settings["enabledPlugins"]![$"{Plugin()}@{name}"]!.GetValue<bool>().ShouldBeTrue();
        Read(".claude", "marketplace", Plugin(), ".claude-plugin", "plugin.json")["name"]!.GetValue<string>().ShouldBe(Plugin());
    }

    /// <summary>The marketplace's one plugin, whose folder is its name.</summary>
    private static string Plugin()
    {
        var plugin = Read(".claude", "marketplace", ".claude-plugin", "marketplace.json")["plugins"]!.AsArray().ShouldHaveSingleItem()!;
        var name = plugin["name"]!.GetValue<string>();
        plugin["source"]!.GetValue<string>().ShouldBe($"./{name}");
        return name;
    }

    private static string[] Command(JsonNode server) =>
        [server["command"]!.GetValue<string>(), .. server["args"]!.AsArray().Select(arg => arg!.GetValue<string>())];

    private static JsonNode Read(params string[] path) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine([_root, .. path])))!;
}
