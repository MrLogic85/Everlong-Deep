---
name: fix-release
description: Ship an Everlong Deep fix release X.Y.Z (Z > 0) from release/vX.Y with its own release notes, and bring the fix and any renumbering to main. Use when the human wants a released version fixed before the next milestone.
---

# Fix release

`docs/RELEASING.md` → "Fix releases" owns the rules. Read
`LOCAL_CONFIG.md` first for the `gh` login and the signing key.

1. Find X.Y.Z, the newest `vX.Y.*` tag plus one patch:

   ```bash
   git fetch --tags origin
   git tag --list 'vX.Y.*' --sort=-v:refname | head -n 1
   ```

2. If a milestone already has that title, rename it and the later
   `X.Y.*` milestones up by one, highest first, since titles are unique,
   and retitle their "Release X.Y.Z" issues to match:

   ```bash
   gh api 'repos/MrLogic85/Everlong-Deep/milestones?per_page=100' -q '.[] | "\(.number) \(.title)"'
   gh api -X PATCH repos/MrLogic85/Everlong-Deep/milestones/<number> -f title=<new>
   ```

3. Branch from `release/vX.Y` and make the fix as usual (the
   `start-issue` skill), then run `.github/scripts/set-version.sh X.Y.Z`
   and write `docs/release-notes/X.Y.Z.md` with the `write-release-notes`
   skill.
4. Ship it with `ship-pr`, but open it with
   `gh pr create --base release/vX.Y …` and skip auto-merge. Merge with
   `gh pr merge --squash` only after `gh pr checks --watch` exits 0; if it
   reports no checks yet, wait and run it again.
5. Publish from the release branch once its checks pass:

   ```bash
   git switch release/vX.Y && git pull --ff-only
   .github/scripts/release.sh --dry-run
   .github/scripts/release.sh
   gh release view vX.Y.Z
   ```

   If the script stops, do what its message says.

6. On `main`, in one PR with `ship-pr`: the fix, if it applies there, and
   `set-version.sh` to the renamed milestone's version if step 2 renamed
   the one `main` carries. If no PR closes the issue, close it by hand.
7. Delete the APK the release left in `build/`, and switch back to `main`.
