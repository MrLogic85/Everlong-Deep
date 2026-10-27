namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/guard-bash.sh denies the git commands AGENTS.md → "Working copy" forbids, work
/// that lands on main without a PR, and bad PR titles, but not text that only mentions them.
/// </summary>
public sealed class GuardBashHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "guard-bash.sh";

    private readonly HookFixture _fixture = fixture;

    private readonly string _feature = fixture.Checkout("feature", "feature");

    /// <summary>Gives the main checkout a local branch and a branch that exists only on a remote.</summary>
    private readonly HookFixture _branches = Branches(fixture);

    [Fact]
    public void InvalidInput_IsAllowed()
    {
        _fixture.RunRaw(_hook, "{bad").ShouldBeEmpty();
    }

    /// <summary>
    /// One row per way the hook reads a command line or decides; a null reason allows. The rows
    /// that commit after a switch prove where the hook thinks HEAD is.
    /// </summary>
    [Theory]
    [InlineData("env -u HOME GIT_TRACE=0 /usr/bin/git --no-pager -C feature stash push -m wip", "git stash is not allowed")]
    [InlineData("ls\ngit worktree add ../copy", "git worktree is not allowed")]
    [InlineData("x=$(git clone https://github.com/MrLogic85/Everlong-Deep.git copy)", "git clone is not allowed")]
    [InlineData("ls; bash -lc 'git reset --hard origin/main'", "git reset --hard is not allowed")]
    [InlineData("if true; then (git status && git -c core.editor=true commit --amend); fi", "Do not commit on main")]
    [InlineData("cd feature && cd .. && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch -c fix && git switch main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -- notes.md && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout notes.md && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -p other && git commit -m Fix", "Do not commit on main")]
    [InlineData("env -C feature git log; git commit -m Fix", "Do not commit on main")]
    [InlineData("env -C feature --chdir . git commit -m Fix", "Do not commit on main")]
    [InlineData("git -C feature switch feature && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch -c fix && git switch -t origin/main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git push", "Do not push to main")]
    [InlineData("git push origin HEAD", "Do not push to main")]
    [InlineData("git push -u origin HEAD:main", "Do not push to main")]
    [InlineData("git push --force origin +feature:refs/heads/main", "Do not push to main")]
    [InlineData("git push origin --delete main", "Do not push to main")]
    [InlineData("git push --all origin", "Do not push to main")]
    [InlineData("gh -R MrLogic85/Everlong-Deep pr create --title \"add hooks\" --body x", "PR title \"add hooks\" fails")]
    [InlineData("gh pr create -t \"chore: Add hooks\"", "PR title \"chore: Add hooks\" fails")]
    [InlineData("gh pr edit 12 --title=\"chore(#12): add hooks\"", "PR title \"chore(#12): add hooks\" fails")]
    [InlineData("gh pr create --fill", "Pass the PR title with --title")]
    [InlineData("echo \"git stash\" 'git push origin main' > notes.txt; git log --grep 'git stash' # git stash", null)]
    [InlineData("cat <<'EOF'\ngit stash\ngit push origin main\nEOF\necho done", null)]
    [InlineData("git stash list; git stash show -p; git worktree list; git reset --soft HEAD~1; git commit --dry-run -m Fix", null)]
    [InlineData("git push --tags; git push origin v0.14.3 release/v0.14 feature", null)]
    [InlineData("gh pr create --title \"chore(#1320): Add hooks\" --body \"git stash\" && gh pr edit 12 --body x", null)]
    [InlineData("gh pr create --title \"$TITLE\" --body-file body.md && gh pr edit 12 --title \"$(head -1 title.txt)\"", null)]
    [InlineData("git switch -c fix && git commit -m Fix && git push -u origin HEAD", null)]
    [InlineData("git switch feature && git commit -m Fix", null)]
    [InlineData("git checkout other && git commit -m Fix", null)]
    [InlineData("git checkout remote-only && git commit -m Fix", null)]
    [InlineData("git checkout HEAD~0 && git commit -m Fix", null)]
    [InlineData("git switch -d other && git commit -m Fix", null)]
    [InlineData("git switch @{-1} && git commit -m Fix", null)]
    [InlineData("git switch --create=fix && git commit -m Fix", null)]
    [InlineData("git switch -cfix && git commit -m Fix", null)]
    [InlineData("git checkout -fb fix && git commit -m Fix", null)]
    [InlineData("git checkout --orphan=fix && git commit -m Fix", null)]
    [InlineData("git switch -t origin/remote-only && git commit -m Fix", null)]
    [InlineData("git checkout other -- && git commit -m Fix", null)]
    [InlineData("env -Cfeature git commit -m Fix; env --chdir=feature git commit -m Fix", null)]
    public void OnMain_Command_IsDeniedForItsReason(string command, string? reason)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command));

        HookFixture.ShouldDecide(output, reason);
    }

    [Theory]
    [InlineData("git commit -F - <<'EOF'\nDeny git stash.\ngit push origin main\nEOF", null)]
    [InlineData("git push && git push -u origin HEAD", null)]
    [InlineData("git checkout main -- notes.md && git checkout main notes.md; git commit -m Fix", null)]
    [InlineData("git -C .. commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout main -- && git commit -m Fix", "Do not commit on main")]
    [InlineData("git checkout -fqb main && git commit -m Fix", "Do not commit on main")]
    [InlineData("git switch --orphan main && git commit -m Fix", "Do not commit on main")]
    [InlineData("cd .. && git push", "Do not push to main")]
    public void OnAFeatureBranch_Command_IsDeniedOnlyInTheMainCheckout(string command, string? reason)
    {
        var output = _fixture.Run(_hook, HookFixture.Bash(command), cwd: _feature);

        HookFixture.ShouldDecide(output, reason);
    }

    private static HookFixture Branches(HookFixture fixture)
    {
        HookFixture.Git(fixture.Root, "branch", "-f", "other");
        HookFixture.Git(fixture.Root, "update-ref", "refs/remotes/origin/remote-only", "HEAD");
        return fixture;
    }
}
