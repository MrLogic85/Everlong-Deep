---
name: ship-pr
description: Commit, push and open an Everlong Deep pull request, enable squash auto-merge, and follow up your open PRs and main's runs. Use when a reviewed change is ready to commit, push or open as a PR, and when a task starts or the session ends.
---

# Ship a PR

`docs/REVIEW.md` owns the rules. Read `LOCAL_CONFIG.md` first
for the `gh` login and push remote.

1. Check every "Definition of Done" box except the required checks, which
   the ruleset waits for. Make a missing manual testing decision with the
   `plan-manual-tests` skill.
2. Commit with the PR title as the subject and the `Co-authored-by:`
   trailers "PR title and commit hygiene" asks for.
3. Push the branch: `git push -u origin <branch>`. If the hook denies it
   for its receipt, run the `review-change` skill again ("Review
   receipts").
4. Write the body from `.github/pull_request_template.md` to
   `.scratch/pr.md`, with everything the "Definition of Done" asks the PR
   to say and "Notes for the reviewer" from the `review-change` skill's
   `.scratch/review.md`, then open the PR, enable auto-merge and delete
   both files:

   ```bash
   gh pr create --title "type(#N): Description" --body-file .scratch/pr.md
   gh pr merge --auto --squash
   ```

   If auto-merge cannot be enabled, do what "Auto-merge" says.

5. Don't wait for the checks or the merge. Switch back and delete the
   local branch, which the remote keeps, then go on to the next task:
   `git switch main && git branch -D <branch>`.
6. Follow up the PRs you opened when the next task starts and before the
   session ends ("Auto-merge"). A new session finds them in the recent
   list; the human's own PRs share the login, so ask if one is unclear.
   For each PR `P`:

   ```bash
   gh pr list --author @me --state all --limit 10 --json number,state,headRefName,title
   gh pr view P --json state,statusCheckRollup,comments,reviews
   gh api repos/MrLogic85/Everlong-Deep/pulls/P/comments
   gh run list --branch main --limit 1 --json conclusion,displayTitle
   ```

   For an open PR with a failing check or an unanswered comment, answer
   it as "Auto-merge" says: `git fetch origin <branch> && git switch
   <branch>`, fix it, run `review-change` and push again, then step 5
   once more. If the latest run on `main` failed, report it and fix it
   forward with a new issue and PR.
7. For a PR found merged, if its issue has a parent, set the parent's
   Status by `docs/ISSUES.md` → "Parent issues".
