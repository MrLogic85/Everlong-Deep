# Issues

GitHub Issues track all work. Issue metadata lives in three places:

- **Labels** say what an issue *is*: its type and the areas it touches.
- **Project fields** in the
  [Everlong Deep project](https://github.com/users/MrLogic85/projects/3) say
  where it *stands*: Status, Priority and Size.
- **GitHub itself** holds milestones (the plan, one per version), sub-issues
  and blocked-by relationships.

## Required metadata

Every open issue has:

- Exactly one `type: ...` label
- At least one `area: ...` label
- A **Priority** and a **Status**
- A **Size** once it is a shaped leaf issue (see "Size")

Backlog work has no milestone. Labels never
repeat GitHub state, milestones or project fields.
`.github/scripts/issue-audit.sh` lists open issues that break this
document's rules.

## Setting project fields

New issues join the project automatically. Read and set fields with
`.github/scripts/issue-fields.sh`:

```bash
.github/scripts/issue-fields.sh 286                       # print Status, Priority, Size
.github/scripts/issue-fields.sh 286 --status Ready --priority Major --size 3
.github/scripts/issue-fields.sh 286 --size none           # clear a field
```

It uses the caller's `gh` login, whose token needs the `project` scope
(`gh auth refresh -h github.com -s project`).

## Creating an issue

Titles describe the problem or outcome, with no `[BUG]`-style prefixes.

File an issue as soon as you spot a problem or improvement outside the
current task, such as a bug, a code smell, duplication or a missing
abstraction, while the evidence is at hand, and tell the human its
number. A remark in a reply, PR or review is not enough.

A **simple** problem (Size 1 or 2, with no open decision, design or
manual testing) is fixed rather than left waiting: fix it in the current
change and name it in the PR, or, with no current change, file it as
**Ready** and start it at once; if it came up in a conversation with the
human, start it only once they agree. Any other problem is not folded
into the current change.

Automation sets only Status **Needs review**, so the creator sets the
rest of "Required metadata" at once, with **Idea** instead if the issue
is not shaped yet. A parent and each issue it waits on are GitHub
sub-issue and blocked-by relationships; "Blocked by #N" in the body does
not record them.

## Labels

`gh label list` shows each label and what it means. CI work is `type: chore` or
`type: bug` plus `area: ci`.

## Priority

Priority measures delivery risk; unsafe process or correctness work
outranks features.

- **1 Critical** — Fix now: broken build or demo, data loss, unusable core
  flow, or a broken delivery gate.
- **2 Major** — Blocks safe progress toward the next milestone,
  including work that protects CI, review, release, architecture
  boundaries or issue quality.
- **3 Default** — Important, but safe work can continue meanwhile.
- **4 Minor** — Nice-to-have, polish or backlog.

## Size

Story points, set once the issue is shaped. **Idea**, `type: question`,
`type: epic` and parent issues have no Size.

- **1** — About a one-liner
- **2** — Touches one or two files
- **3** — Touches several files, straightforward
- **5** — Large but manageable; needs complex thinking
- **8** — Too big: split it into sub-issues before it can become
  **Ready**, then clear its Size and estimate only the sub-issues

## Status

- **Idea** — Unshaped. When discussion gives it one clear outcome and
  acceptance criteria, set **Needs review**.
- **Needs review** — Shaped; review it before implementation (see
  "Reviewing an issue").
- **Needs design** — Waits for a design proposal that the owner
  approves, recorded on the issue. Then set **Needs review**, or **Ready**
  if the issue already passed review and the design changes neither its
  scope nor its acceptance criteria.
- **Needs decision** — A product or design choice must be settled first.
- **Blocked** — Waits on another issue, recorded as blocked-by. A wait on
  an external tool or person gets its own issue to block on. When the
  last blocker closes, set **Ready**.
- **Ready** — Reviewed, or simple (see "Creating an issue"), and
  actionable, with no open blocked-by issue.
- **In progress** — Set it when you start.
- **Done** — Set automatically when the issue closes.

### Parent issues

A parent issue (one with sub-issues) only tracks them; it is not reviewed
or implemented itself. Its Status follows its open sub-issues:

- **In progress** once any sub-issue is In progress or closed.
- Otherwise **Ready** once every open sub-issue is Ready or Blocked.
- Otherwise the least-shaped of their statuses: Idea, then Needs design,
  Needs decision and Needs review.

A parent has no blocked-by issues: record each wait on the sub-issue that
waits, and when a blocked issue is split, move its blockers to the new
sub-issues.

## Reviewing an issue

Simple issues (see "Creating an issue") can be worked directly. Review
any other issue before implementation when it:

- might need splitting;
- affects architecture, persistence, simulation balance, determinism,
  CI or release;
- is ambiguous about behaviour or acceptance criteria;
- likely needs manual testing; or
- is in **Needs review** or **Needs decision**.

`issue-reviewer` (`.claude/agents/issue-reviewer.md`) owns what to check
and the finding levels. The issue body records what the review changed,
and a comment its summary. The result sets the Status:

- A Major or Medium finding left open: **Needs decision** when it waits
  on the owner's choice, otherwise **Needs review**; review it again once
  the body is fixed.
- Otherwise: **Needs design** if it changes what the user sees without
  an approved design, else **Blocked** if it has an open blocker, else
  **Ready**. A Size 8 issue's sub-issues take this Status, and the parent
  follows "Parent issues".
