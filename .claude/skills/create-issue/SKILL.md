---
name: create-issue
description: File a new Everlong Deep GitHub issue with its labels, project fields, milestone, parent and blockers. Use whenever you file a bug, improvement, decision or follow-up, or split an issue.
---

# Create an issue

`docs/ISSUES.md` owns the rules. Read `LOCAL_CONFIG.md` first for the
`gh` login.

1. If it is a simple problem found outside the current task
   (`docs/ISSUES.md` → "Creating an issue"), fix it in your current change
   and stop; with no current change, file it as a *quick fix* below.
2. Search for a duplicate: `gh issue list --state all --search "<words>"`.
   If one exists, comment there instead; for a quick fix, also set it
   **Ready** unless it is In progress, and start it as step 7 says.
3. Write the body to `.scratch/issue.md`: the outcome, the context with
   evidence (files, lines, logs, issue numbers), acceptance criteria and a
   test plan. Title it as `docs/ISSUES.md` → "Creating an issue"
   says.
4. Create it with the labels "Required metadata" asks for, the milestone
   if it belongs to one, and its parent and blockers:

   ```bash
   gh issue create --title "…" --body-file .scratch/issue.md \
     --label "type: …" --label "area: …" [--milestone "0.x.y"] \
     [--parent P] [--blocked-by B1,B2]
   ```

   Then delete `.scratch/issue.md`.
5. Set Status, Priority and Size as "Creating an issue", "Status" and
   "Size" say, Status **Ready** for a quick fix:
   `.github/scripts/issue-fields.sh N --status … --priority … [--size …]`.
6. Run `.github/scripts/issue-audit.sh` and fix what it lists for the new
   issue.
7. Tell the human its number, and start a quick fix with the
   `start-issue` skill: at once, or once the human agrees if it came up
   in a conversation with them.
