using System.Text.Json.Nodes;

namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/reviewer-lsp.sh lets a review agent use the LSP tool only for lookups, so it
/// stays read-only if the tool gains an operation that writes. See docs/REVIEW.md → "Hooks".
/// </summary>
public sealed class ReviewerLspHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "reviewer-lsp.sh";

    [Theory]
    [InlineData("hover", null)]
    [InlineData("outgoingCalls", null)]
    [InlineData("rename", "LSP rename is not allowed for a review agent")]
    [InlineData(null, "LSP without an operation is not allowed for a review agent")]
    public void Operation_IsAllowedOnlyForALookup(string? operation, string? reason) =>
        HookFixture.ShouldDecide(fixture.Run(_hook, Payload(operation)), reason);

    private static JsonObject Payload(string? operation)
    {
        var input = new JsonObject { ["file"] = "libs/A.cs", ["line"] = 6, ["character"] = 10 };
        if (operation is not null)
        {
            input["operation"] = operation;
        }

        return new JsonObject { ["hook_event_name"] = "PreToolUse", ["tool_name"] = "LSP", ["tool_input"] = input };
    }
}
