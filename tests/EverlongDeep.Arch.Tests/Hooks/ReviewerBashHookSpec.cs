namespace EverlongDeep.Arch.Tests.Hooks;

/// <summary>
/// .claude/hooks/reviewer-bash.sh lets a review agent run only commands that read, and denies
/// the rest with a reason. See docs/REVIEW.md → "Hooks".
/// </summary>
public sealed class ReviewerBashHookSpec(HookFixture fixture) : IClassFixture<HookFixture>
{
    private const string _hook = "reviewer-bash.sh";

    /// <summary>Each row runs the reading commands of one kind together; any deny fails it.</summary>
    [Theory]
    [InlineData("git diff --cached \"$(git merge-base main HEAD)\" && git --no-pager log --oneline -5 2>&1 && git -C libs show HEAD:README.md | head -40; git log $(git merge-base main HEAD)..HEAD")]
    [InlineData("git status --short >/dev/null; git grep -n -e '-O2' -- '*.cs'; git blame -L 10,20 a.cs; git ls-files -o --exclude-standard; ls 2>&1 >&-")]
    [InlineData("git branch -a --contains HEAD; git branch --list 'fix/*'; git tag -l 'v*'; git config --get user.name; git config get user.name")]
    [InlineData("git stash list; git remote -v; git remote get-url origin; git reflog -5; git cherry -v main; git --version; /usr/bin/git log -1")]
    [InlineData("cd libs && ls -la && wc -l *.cs; grep -rn Foo docs | sort | uniq -c | sort -rn | head; uniq -f 1 a.txt; sed -n '1,40p;$p' a.md; find . -name '*.cs' -newer a.txt; rg -n 'class Foo' libs")]
    [InlineData("x=$(git rev-parse HEAD); for f in a b; do cat \"$f\"; done; while read -r line; do echo \"$line\"; done < list.txt; printf '%s\\n' a; test -f a; [ -n x ]; GIT_PAGER=cat git log -1")]
    [InlineData("export GH_CONFIG_DIR=$HOME/.config/gh; gh issue view 1321 --comments; gh -R MrLogic85/Everlong-Deep pr diff 12; gh pr checks 12; gh search issues receipt; gh auth status")]
    [InlineData("gh api repos/MrLogic85/Everlong-Deep/issues/1321 --jq .title; gh api -X GET search/issues -f q=receipt; gh api graphql -f query='{ viewer { login } }'")]
    [InlineData("dotnet build EverlongDeep.slnx -warnaserror; dotnet test tests/EverlongDeep.Arch.Tests --no-build -c Release --filter 'Name~Hooks' -v q --no-incremental --logger 'console;verbosity=detailed'; dotnet format EverlongDeep.slnx --verify-no-changes --no-restore -v q --severity=warn --include=src")]
    [InlineData("bash -n .claude/hooks/guard-bash.sh && shellcheck .claude/hooks/*.sh; .github/scripts/review-receipt.sh check --worktree; echo $?; .github/scripts/review-receipt.sh base")]
    [InlineData("cat <<'EOF'\nrm -rf x\nEOF\ncat <<EOF\n$(git rev-parse HEAD)\nEOF")]
    [InlineData("echo \"$(git log -1 --format=%s <<'EOF'\nDon't\nEOF\n)\"")]
    [InlineData("echo \"$(cat <<<x\nls)\"")]
    [InlineData("echo $((1 + 2)) \"$((2 * 3))\" $(( (1 + 2) * 3 )) \"$(( (a) + (b) ))\"; (( 1 << 2 )) && ((x = 4 / 2)) && ls; echo \"$(echo $((1<<2))\nls)\"")]
    public void ReadingCommand_IsAllowed(string command)
    {
        var output = fixture.Run(_hook, HookFixture.Bash(command));

        HookFixture.DenyReason(output).ShouldBeNull();
    }

    /// <summary>One row per way a command line can hide a write from the parser.</summary>
    [Theory]
    [InlineData("echo x > notes.md", "Writing to notes.md")]
    [InlineData("ls &> out.txt", "Writing to out.txt")]
    [InlineData("ls 2> err.txt", "Writing to err.txt")]
    [InlineData("ls >| out.txt", "Writing to out.txt")]
    [InlineData("cat <> out.txt", "Writing to out.txt")]
    [InlineData("cat <<'EOF' > a.cs\nclass A {}\nEOF", "Writing to a.cs")]
    [InlineData("echo x > 'a>&1'", "Writing to a>&1")]
    [InlineData("echo x >-", "Writing to -")]
    [InlineData("ls > $(echo f)", "Writing to $(echo f)")]
    [InlineData("<&- mv a cat", "mv")]
    [InlineData("echo $(touch x)", "touch")]
    [InlineData("echo `touch x`", "touch")]
    [InlineData("cat <(touch x)", "touch")]
    [InlineData("echo \"$(touch x)\"", "touch")]
    [InlineData("cat \"`rm f`\"", "rm")]
    [InlineData("echo $(( $(rm f) + 1 ))", "rm")]
    [InlineData("cat <<EOF\n$(echo hi\nrm x)\nEOF", "rm")]
    [InlineData("cat <<EOF\n`echo hi\nrm x`\nEOF", "rm")]
    [InlineData("echo \"$(echo ')' ; rm x)\"", "rm")]
    [InlineData("echo \"$(echo $'\\')' ; rm x)\"", "rm")]
    [InlineData("echo $'\\'' ; rm x ; echo $'\\''", "rm")]
    [InlineData("echo \"$(echo hi # )\nrm x)\"", "rm")]
    [InlineData("echo \"$(cat <<EOF\n)\nEOF\nrm x)\"", "rm")]
    [InlineData("echo `echo \\`rm x\\``", "rm")]
    [InlineData("echo \"`echo \\`rm x\\``\"", "rm")]
    [InlineData("echo $((true)&&(rm x))", "rm")]
    [InlineData("echo \"$((true) && (rm x))\"", "rm")]
    [InlineData("((x=1<<2))\nrm y", "rm")]
    [InlineData("echo \"$(echo \"$(echo \"$(echo \"$(ls)\")\")\")\"", "A substitution nested this deep")]
    [InlineData("echo \"$(ls\"", "A substitution that never closes")]
    [InlineData("echo $(ls", "A substitution that never closes")]
    public void HiddenWrite_IsDenied(string command, string what) => AssertRefused(command, what);

    /// <summary>One row per program, option or assignment the hook refuses, by the check that refuses it.</summary>
    [Theory]
    [InlineData("rm -rf libs", "rm")]
    [InlineData("bash -c 'git log'", "bash")]
    [InlineData("./cat x", "./cat")]
    [InlineData("/tmp/x/.github/scripts/review-receipt.sh check HEAD", "/tmp/x/.github/scripts/review-receipt.sh")]
    [InlineData(".github/scripts/review-receipt.sh write --reviewers a f.md", "review-receipt.sh write")]
    [InlineData("PAGER=less git log", "Setting PAGER")]
    [InlineData("GIT_EXTERNAL_DIFF=./x git diff", "Setting GIT_EXTERNAL_DIFF")]
    [InlineData("export PAGER=less", "Setting PAGER")]
    [InlineData("export", "export without arguments")]
    [InlineData("export -p", "export -p")]
    [InlineData("EVERLONG_DEEP_UPDATE_PINS=1 dotnet test", "Setting EVERLONG_DEEP_UPDATE_PINS")]
    [InlineData("GH_BROWSER=x gh pr view 12 --web", "Setting GH_BROWSER")]
    [InlineData("git commit -m Fix", "git commit")]
    [InlineData("git stash", "git stash")]
    [InlineData("git -c core.pager=less log", "git -c")]
    [InlineData("git diff --output=review.patch", "git diff --output=review.patch")]
    [InlineData("git branch fix", "git branch fix")]
    [InlineData("git branch --list --merged -D x", "git branch -D")]
    [InlineData("git config user.name x", "git config without --get or --list")]
    [InlineData("git config user.name get", "git config without --get or --list")]
    [InlineData("git config --unset user.name", "git config --unset")]
    [InlineData("git grep -O foo", "git grep -O")]
    [InlineData("git grep -nOtouch -e y", "git grep -nOtouch")]
    [InlineData("git grep --open=sh -e x", "git grep --open=sh")]
    [InlineData("git remote -v add x y", "git remote add")]
    [InlineData("git reflog expire --all", "git reflog expire")]
    [InlineData("gh pr merge 12 --squash", "gh pr merge")]
    [InlineData("gh -R", "gh -R")]
    [InlineData("gh auth status -t", "gh auth status -t")]
    [InlineData("gh api -X PATCH repos/o/r/issues/1", "gh api -X PATCH")]
    [InlineData("gh api -XPOST repos/o/r/labels", "gh api -X POST")]
    [InlineData("gh api --method=POST repos/o/r/labels", "gh api -X POST")]
    [InlineData("gh api repos/o/r/labels -f name=x", "gh api with -f or -F but no -X GET")]
    [InlineData("gh api -fname=x repos/o/r/labels", "gh api with -f or -F but no -X GET")]
    [InlineData("gh api --input body.json repos/o/r/issues", "gh api --input")]
    [InlineData("gh api graphql -f query='mutation { closeIssue }'", "A GraphQL mutation")]
    [InlineData("find . -exec rm {} +", "find -exec")]
    [InlineData("find . -name '*.tmp' -delete", "find -delete")]
    [InlineData("sed -i '' s/a/b/ a.cs", "sed -i")]
    [InlineData("sed -n 's/a/b/w out' a.cs", "sed 's/a/b/w out'")]
    [InlineData("sed 1,5p a.cs", "sed without -n")]
    [InlineData("sort --out=x a", "sort --out=x")]
    [InlineData("sort -ro x a", "sort -ro")]
    [InlineData("sort --compress-program=sh a", "sort --compress-program=sh")]
    [InlineData("uniq a.txt out.txt", "uniq with an output file")]
    [InlineData("uniq - out.txt", "uniq with an output file")]
    [InlineData("file -bC", "file -bC")]
    [InlineData("rg --pre ./run.sh foo", "rg --pre")]
    [InlineData("rg --hostname-bin=./x foo", "rg --hostname-bin")]
    [InlineData("dotnet build -p:PostBuildEvent=x", "dotnet build -p")]
    [InlineData("dotnet build /bl:x.binlog", "dotnet build /bl")]
    [InlineData("dotnet test --logger trx", "dotnet test --logger trx")]
    [InlineData("dotnet format", "dotnet format without --verify-no-changes")]
    [InlineData("dotnet format --verify-no-changes --report out", "dotnet format --report")]
    [InlineData("dotnet run --project x", "dotnet run")]
    [InlineData("printf -v 'a[$(rm x)]' x", "printf -v")]
    [InlineData("read 'a[$(rm x)]' < f", "read a[$(rm x)]")]
    [InlineData("test -v 'a[$(rm x)]'", "test -v")]
    [InlineData("[ -v 'a[$(rm x)]' ]", "[ -v")]
    [InlineData("[[ -n x ]]", "[[")]
    public void WritingCommand_IsDenied(string command, string what) => AssertRefused(command, what);

    private void AssertRefused(string command, string what) =>
        HookFixture.ShouldDecide(fixture.Run(_hook, HookFixture.Bash(command)), $"{what} is not allowed for a review agent");
}
