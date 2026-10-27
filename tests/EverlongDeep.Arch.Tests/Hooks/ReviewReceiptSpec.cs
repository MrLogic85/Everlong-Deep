namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// guard-bash.sh denies the push of a branch whose change has no receipt from
/// .github/scripts/review-receipt.sh, and any change after the review needs a new one.
/// See docs/REVIEW.md → "Review receipts". Each spec gets its own repository on a feature branch.
/// </summary>
public sealed class ReviewReceiptSpec : IDisposable
{
    private const string _hook = "guard-bash.sh";
    private const string _script = ".github/scripts/review-receipt.sh";
    private const string _noReceipt = "No review receipt matches feature";

    private readonly HookFixture _fixture = new("feature");

    private string Root => _fixture.Root;

    /// <summary>Kept in the git dir, so it is not part of the change it describes.</summary>
    private string Findings => Path.Combine(Root, ".git", "findings.md");

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Push_WithoutAReview_IsDenied()
    {
        Commit("a.txt", "one");

        DenyReason("git push -u origin HEAD").ShouldNotBeNull().ShouldStartWith(_noReceipt);
    }

    [Fact]
    public void Push_AfterTheReview_IsAllowed()
    {
        Commit("a.txt", "one");
        Write("a.txt", "two");
        Review();

        DenyReason("git add -A && git commit -m Fix && git push").ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Push_AfterAnEditOrModeChangeSinceTheReview_IsDenied(bool modeOnly)
    {
        Commit("a.txt", "one");
        Review();
        if (modeOnly)
        {
            HookFixture.Git(Root, "update-index", "--chmod=+x", "a.txt");
            HookFixture.Git(Root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "commit", "-q", "-m", "Mode");
        }
        else
        {
            Commit("a.txt", "two");
        }

        DenyReason("git push").ShouldNotBeNull().ShouldStartWith(_noReceipt);
    }

    /// <summary>origin/main moves while the local main stays behind.</summary>
    [Theory]
    [InlineData("main")]
    [InlineData("origin/main")]
    public void Push_OfTheReviewedChangeRebasedOntoANewerMain_IsAllowed(string onto)
    {
        Commit("a.txt", "one");
        Review();
        HookFixture.Git(Root, "switch", "-q", "-c", "upstream", "main");
        Commit("b.txt", "main moved on");
        HookFixture.Git(Root, "update-ref", onto == "main" ? "refs/heads/main" : "refs/remotes/origin/main", "upstream");
        HookFixture.Git(Root, "switch", "-q", "feature");
        HookFixture.Git(Root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "rebase", "-q", onto);

        DenyReason("git push --force-with-lease").ShouldBeNull();
    }

    [Fact]
    public void Push_WhenTheReceiptCheckFails_IsDenied()
    {
        Commit("a.txt", "one");
        HookFixture.Git(Root, "branch", "-D", "main");

        DenyReason("git push").ShouldNotBeNull().ShouldStartWith("The review receipt check failed for feature");
    }

    [Fact]
    public void Push_InAnotherRepository_IsNotChecked()
    {
        var other = _fixture.Checkout("other", "feature");
        File.WriteAllText(Path.Combine(other, "a.txt"), "one\n");
        HookFixture.Git(other, "add", "-A");
        HookFixture.Git(other, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "commit", "-q", "-m", "Set a.txt");

        DenyReason("git -C other push").ShouldBeNull();
    }

    /// <summary>
    /// The review covers a staged edit of a.txt but not the untracked notes.txt; a commit earlier
    /// in the line is measured by what it takes: the index, the tracked files, or everything after
    /// `git add -A`.
    /// </summary>
    [Theory]
    [InlineData(false, "git commit -m Fix && git push", false)]
    [InlineData(true, "git commit -sam Fix; git push origin HEAD", false)]
    [InlineData(true, "git commit -mfix-test -a && git push", false)]
    [InlineData(true, "git commit --all -m Fix && git push -u origin feature", false)]
    [InlineData(true, "git commit -uall -m Fix && git push", true)]
    [InlineData(false, "git add -A && git commit -m Notes && git push", true)]
    [InlineData(false, "git add -A && git commit -am Notes && git push", true)]
    public void CommitAndPush_IsMeasuredByWhatTheCommitTakes(bool unstage, string command, bool denied)
    {
        Commit("a.txt", "one");
        Write("a.txt", "two");
        HookFixture.Git(Root, "add", "a.txt");
        Write("notes.txt", "not part of the change");
        ReviewStaged();
        if (unstage)
        {
            HookFixture.Git(Root, "reset", "-q");
        }

        HookFixture.ShouldDecide(_fixture.Run(_hook, HookFixture.Bash(command)), denied ? _noReceipt : null);
    }

    [Theory]
    [InlineData("git rebase main && git push --force-with-lease", "rebase")]
    [InlineData("git reset --soft HEAD~1 && git push --force-with-lease", "reset")]
    [InlineData("git branch -f feature main && git push origin feature", "branch")]
    [InlineData("git fetch origin main:feature && git push origin feature", "fetch")]
    [InlineData("git switch -C feature main && git push", "switch")]
    [InlineData("git switch -c other main && git push -u origin other", "switch")]
    [InlineData("git commit -F msg.txt a.txt && git push", "commit")]
    [InlineData("git add -A && git commit -o -m Fix -- a.txt && git push", "commit")]
    [InlineData("git add a.txt && git commit -m Fix && git push", "add")]
    [InlineData("git add -A src && git commit -m Fix && git push", "add")]
    public void Push_AfterHeadMovesInTheSameLine_IsDenied(string command, string moved)
    {
        Commit("a.txt", "one");
        Review();

        DenyReason(command).ShouldNotBeNull().ShouldStartWith($"Run git push as its own command after git {moved}");
    }

    [Fact]
    public void Push_OfAnotherUnreviewedBranch_IsDenied()
    {
        Commit("a.txt", "one");
        Review();
        HookFixture.Git(Root, "switch", "-q", "-c", "other");
        Commit("b.txt", "more");
        HookFixture.Git(Root, "switch", "-q", "feature");

        DenyReason("git push origin feature other").ShouldNotBeNull().ShouldStartWith("No review receipt matches other");
    }

    [Theory]
    [InlineData(false, "git push origin HEAD~0:other", "HEAD~0")]
    [InlineData(true, "git push origin HEAD:other", "HEAD")]
    [InlineData(false, "git push origin +HEAD:feature", "feature")]
    [InlineData(false, "git switch -c other && git push -u origin other", "other")]
    [InlineData(false, "git commit -m \"$(cat <<'EOF'\nDon't\nEOF\n)\" && git push", "feature")]
    [InlineData(false, "((x=1<<2))\ngit push", "feature")]
    public void Push_OfAnUnreviewedCommit_HoweverTheLineNamesIt_IsDenied(bool detached, string command, string name)
    {
        Commit("a.txt", "one");
        if (detached)
        {
            HookFixture.Git(Root, "switch", "-q", "--detach");
        }

        DenyReason(command).ShouldNotBeNull().ShouldStartWith($"No review receipt matches {name}");
    }

    [Theory]
    [InlineData("git push --dry-run")]
    [InlineData("git push origin --delete feature")]
    [InlineData("git push origin :feature")]
    [InlineData("git push origin v1")]
    [InlineData("git push origin HEAD:refs/tags/v2")]
    public void Push_ThatSendsNoReviewableBranch_IsAllowed(string command)
    {
        Commit("a.txt", "one");
        HookFixture.Git(Root, "tag", "v1");

        DenyReason(command).ShouldBeNull();
    }

    [Fact]
    public void Write_RecordsTheReviewersAndFindings_AndRemovesReceiptsOlderThanThirtyDays()
    {
        var receipts = Path.Combine(Root, ".git", "review-receipts");
        Directory.CreateDirectory(receipts);
        var old = Path.Combine(receipts, "old.md");
        File.WriteAllText(old, "# Review receipt\n");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-31));
        var recent = Path.Combine(receipts, "recent.md");
        File.WriteAllText(recent, "# Review receipt\n");
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-29));
        Commit("a.txt", "one");

        Review();

        File.Exists(old).ShouldBeFalse();
        File.Exists(recent).ShouldBeTrue();
        var receipt = Directory.GetFiles(receipts).Where(path => path != recent).ShouldHaveSingleItem();
        var text = File.ReadAllText(receipt);
        text.ShouldContain("- Branch: feature\n");
        text.ShouldContain($"- Base: {HookFixture.Git(Root, "rev-parse", "main")}");
        text.ShouldContain($"- Tree: {HookFixture.Git(Root, "rev-parse", "HEAD^{tree}")}");
        text.ShouldContain("- Reviewers: docs-reviewer,tests-reviewer\n");
        text.ShouldContain("Minor, new: dismissed, the wording is intended.");
    }

    [Fact]
    public void Write_OfAChangeNotStagedInFull_ExitsTwo()
    {
        Commit("a.txt", "one");
        File.WriteAllText(Findings, "No findings.\n");
        Write("a.txt", "two");

        var (exitCode, output) = HookFixture.Script(Root, _script, "write", "--reviewers", "docs-reviewer", Findings);

        exitCode.ShouldBe(2);
        output.ShouldContain("git add -A");
    }

    [Fact]
    public void WriteAndCheck_WhenGitCannotReadATrackedFile_SaySo()
    {
        Commit("a.txt", "one");
        File.WriteAllText(Findings, "No findings.\n");
        Write("a.txt", "a longer second version");
        if (!TryMakeUnreadable("a.txt"))
        {
            return;
        }

        var write = HookFixture.Script(Root, _script, "write", "--reviewers", "docs-reviewer", Findings);
        var check = HookFixture.Script(Root, _script, "check", "--worktree");

        write.ExitCode.ShouldBe(2);
        write.Output.ShouldContain("cannot compare the working tree with the index");
        check.ExitCode.ShouldBe(2);
        check.Output.ShouldContain("cannot measure --worktree against main");
        check.Output.ShouldContain("a.txt");
        check.Output.ShouldContain("Permission denied");
    }

    [Fact]
    public void Check_WhenGitWarnsWhileStaging_PrintsNothing()
    {
        Commit("a.txt", "one");
        HookFixture.Git(Root, "config", "core.autocrlf", "true");
        Write("a.txt", "two\n");
        HookFixture.Git(Root, "add", "a.txt");
        ReviewStaged();
        // A newer timestamp makes git re-read the unchanged file, and warn again, when check stages it.
        File.SetLastWriteTimeUtc(Path.Combine(Root, "a.txt"), DateTime.UtcNow.AddMinutes(1));

        var (exitCode, output) = HookFixture.Script(Root, _script, "check", "--worktree");

        exitCode.ShouldBe(0);
        output.ShouldBeEmpty();
    }

    [Fact]
    public void Base_PrintsWhereTheBranchLeftMain()
    {
        Commit("a.txt", "one");

        var (exitCode, output) = HookFixture.Script(Root, _script, "base");

        exitCode.ShouldBe(0);
        output.ShouldBe(HookFixture.Git(Root, "rev-parse", "main"));
    }

    [Fact]
    public void Write_OfAnEmptyChange_RecordsNothing()
    {
        File.WriteAllText(Findings, "No findings.\n");

        var (exitCode, output) = HookFixture.Script(Root, _script, "write", "--reviewers", "docs-reviewer", Findings);

        exitCode.ShouldBe(0);
        output.ShouldContain("nothing to record");
        Directory.Exists(Path.Combine(Root, ".git", "review-receipts")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("write", "FINDINGS")]
    [InlineData("write", "--reviewers", "docs-reviewer", "BLANK")]
    [InlineData("write", "--reviewers", "no-such-reviewer", "FINDINGS")]
    [InlineData("check")]
    [InlineData("check", "no-such-rev")]
    public void Script_WithoutWhatItNeeds_ExitsTwo(params string[] arguments)
    {
        Commit("a.txt", "one");
        File.WriteAllText(Findings, "No findings.\n");
        var blank = Path.Combine(Root, ".git", "blank.md");
        File.WriteAllText(blank, " \n\n");

        var (exitCode, _) = HookFixture.Script(Root, _script, [.. arguments.Select(a => a switch
        {
            "FINDINGS" => Findings,
            "BLANK" => blank,
            _ => a,
        })]);

        exitCode.ShouldBe(2);
    }

    /// <summary>Takes every permission from a file in the fixture; false where that cannot stop git
    /// reading it: on Windows, and for the root user.</summary>
    private bool TryMakeUnreadable(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var path = Path.Combine(Root, name);
        File.SetUnixFileMode(path, UnixFileMode.None);
        return !CanRead(path);
    }

    private static bool CanRead(string path)
    {
        try
        {
            File.OpenRead(path).Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string? DenyReason(string command) => HookFixture.DenyReason(_fixture.Run(_hook, HookFixture.Bash(command)));

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(Root, path), text + "\n");

    private void Commit(string path, string text)
    {
        Write(path, text);
        HookFixture.Git(Root, "add", "-A");
        HookFixture.Git(Root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "commit", "-q", "-m", $"Set {path}");
    }

    private void Review()
    {
        HookFixture.Git(Root, "add", "-A");
        ReviewStaged();
    }

    private void ReviewStaged()
    {
        File.WriteAllText(Findings, "- docs-reviewer: Minor, new: dismissed, the wording is intended.\n- tests-reviewer: No findings.\n");
        var (exitCode, output) = HookFixture.Script(Root, _script, "write", "--reviewers", "docs-reviewer,tests-reviewer", Findings);
        exitCode.ShouldBe(0, output);
    }
}
