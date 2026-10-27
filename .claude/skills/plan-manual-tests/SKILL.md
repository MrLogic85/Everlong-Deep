---
name: plan-manual-tests
description: Decide whether an Everlong Deep change needs manual testing and write its manual test plan into the issue. Use when starting or shaping an issue that may change what a user sees or does, or before opening a PR whose issue says nothing about manual testing.
---

# Plan manual tests

`docs/MANUAL_TESTING.md` owns the rules. Read `LOCAL_CONFIG.md` first
for the `gh` login.

1. Read the issue and what the change touches, and decide with
   `docs/MANUAL_TESTING.md` → "Decision point". If no manual test is
   needed, say why in one line: in the issue while shaping it, otherwise
   in the PR's **How to verify**. Stop there.
2. Pick where each check runs: a headless simulation run with its log,
   or the human for what needs subjective judgement, such as whether a
   story reads as believable.
3. Write steps a reader can repeat. Name the world or events each step
   needs and where they come from, the seed, the settings and what is
   expected, with one check per behaviour the issue changes.
4. Add the plan to the issue body in the format of "What belongs in an
   issue". Fetch the body first and stop if that fails or the file is
   empty, since the edit replaces the whole body:

   ```bash
   mkdir -p .scratch && gh issue view N --json body -q .body > .scratch/issue-N.md &&
     test -s .scratch/issue-N.md
   # append the plan to .scratch/issue-N.md, then:
   gh issue edit N --body-file .scratch/issue-N.md && rm .scratch/issue-N.md
   ```

5. Once the change is built, run the plan and record the evidence in
   the PR's **How to verify**.
