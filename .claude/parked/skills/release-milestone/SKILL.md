---
name: release-milestone
description: Release an Everlong Deep milestone X.Y.Z from main, from the release notes through the published GitHub release to bumping main to the next version. Use when the human asks to release or publish a version.
---

# Release a milestone

`docs/RELEASING.md` → "Releasing a milestone" owns the rules and the
PRs' titles. Read `LOCAL_CONFIG.md` first for the `gh` login and the
signing key.

1. Stop unless the human has asked for this release. Find the release
   issue `#N` and check it is the milestone's last open issue; if others
   are open, stop and report them:

   ```bash
   gh issue list --milestone X.Y.Z --state open
   ```

2. Write the notes with the `write-release-notes` skill and ship them as
   the notes PR with the `ship-pr` skill. Unlike other PRs, wait for this
   one, `P` being the number `gh pr create` printed: `release.sh` needs
   its merge commit's checks to pass on `main`. If a check fails, fix it
   as `ship-pr` step 6 says and wait again; if it reports no checks yet,
   wait and run it again.

   ```bash
   gh pr checks P --watch --fail-fast || exit 1
   until s=$(gh pr view P --json state -q .state) && [ "$s" != OPEN ]; do sleep 15; done
   [ "$s" = MERGED ] || exit 1   # closed unmerged: stop and ask the human
   sha=$(gh pr view P --json mergeCommit -q .mergeCommit.oid)
   until id=$(gh run list --commit "$sha" --limit 1 --json databaseId -q '.[0].databaseId') && [ -n "$id" ]; do sleep 10; done
   gh run watch "$id" --exit-status
   ```

3. Dry-run the release from an up-to-date `main`. It exports and checks
   the APK, but tags and publishes nothing:

   ```bash
   git switch main && git pull --ff-only
   .github/scripts/set-version.sh   # prints version X.Y.Z
   .github/scripts/release.sh --dry-run
   ```

4. Publish, then check the release page shows the notes and the APK. If
   the script stops, do what its message says ("Publishing safeguards").

   ```bash
   .github/scripts/release.sh
   gh release view vX.Y.Z
   ```

5. Find `<next>`, the lowest open milestone after X.Y.Z; if there is
   none, ask the human. On a branch, run
   `.github/scripts/set-version.sh <next>` and ship it as the bump PR
   with `ship-pr`.

   ```bash
   gh api 'repos/MrLogic85/Everlong-Deep/milestones?per_page=100' -q '.[].title' | sort -V
   ```

6. Delete the APK the release left in `build/`.
