---
name: write-release-notes
description: Write docs/release-notes/X.Y.Z.md, the player-facing text and screenshots of an Everlong Deep release. Use when a milestone or fix release needs its release notes, or when asked to draft or change them.
---

# Write release notes

Read `.claude/rules/release-notes.md` first: it owns what goes in the
notes. Read `LOCAL_CONFIG.md` for the `gh` login.

1. List the rule's "Sources". For a milestone, `prev` is the newest
   release reachable from `main`, and the fixes already shipped are on
   its release branch:

   ```bash
   git fetch -q --tags origin
   prev=$(git describe --tags --abbrev=0 origin/main)   # e.g. v0.14.2
   gh issue list --milestone X.Y.Z --state closed --limit 200 --json number,title,labels
   git log --oneline "$prev"..origin/main
   git log --oneline "$prev"..origin/release/"${prev%.*}"   # fixes already shipped
   ```

   For a fix release, list the fix branch's commits since the newest
   `vX.Y.*` tag.
2. Choose and check what goes in by the rule's "Focus", "Fixes" and
   "Check", then write `docs/release-notes/X.Y.Z.md` in its "Shape".
3. Take the pictures the rule's "Images" asks for with the `android-check`
   skill, then upload each and use the `https://github.com/user-attachments/…`
   URL it returns. The token goes through standard input, so it never
   shows in the process list:

   ```bash
   png=.scratch/notes-1.png
   repo_id=$(gh api repos/MrLogic85/Everlong-Deep -q .id)
   gh auth token | sed 's/^/Authorization: Bearer /' | curl --fail-with-body -sS -X POST \
     https://uploads.github.com/user-attachments/assets \
     --url-query "name=$(basename "$png")" --url-query content_type=image/png \
     --url-query "repository_id=$repo_id" -H @- \
     -H "Content-Type: application/octet-stream" --data-binary "@$png" | jq -r .url
   ```

4. Delete the local pictures.
