---
name: review-issue
description: Review an Everlong Deep GitHub issue before implementation with the issue-reviewer agent, act on the findings and set its Status. Use when an issue is in Needs review or Needs decision, or is broad, risky or ambiguous.
---

# Review an issue

`docs/ISSUES.md` → "Reviewing an issue" owns the rules. Read
`LOCAL_CONFIG.md` first for the `gh` login.

1. Run the `issue-reviewer` agent with the issue URL.
2. Act on each finding by its level. Settle what you can from the code,
   docs and earlier discussion. Ask the human a product or design choice
   you cannot settle; if nobody answers, post it as an issue comment.
3. If the review changes the issue, edit its body, and its labels or
   relationships per `docs/ISSUES.md` →
   "Required metadata". File other problems it found with
   the `create-issue` skill.
4. Post the review summary as an issue comment, saying what changed.
5. Set or confirm its Size, and split a Size 8 (`docs/ISSUES.md` →
   "Size").
6. Set the Status that follows, with `.github/scripts/issue-fields.sh`;
   after a split, on the sub-issues, and on the parent by "Parent issues".
