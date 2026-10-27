# Review process

How a change lands in `main`. The `start-issue`, `review-change` and
`ship-pr` skills hold the steps; this document holds the rules.

Every PR is reviewed by the review agents before it is pushed. Human
review is optional, and `nit:` comments don't block.

## Definition of Done

A PR is mergeable when every box is true, or the PR says why a box does
not apply:

- [ ] A linked GitHub Issue and a closing keyword
- [ ] Required checks green (see "Merge gates")
- [ ] Tests as `docs/TEST_STRATEGY.md` asks
- [ ] Review agents ran on the final diff, and the author judged every
      new finding: a Major one is fixed, any other is fixed or dismissed
      with a short reason. Each preexisting finding is fixed here if it
      is simple (`docs/ISSUES.md` → "Creating an issue"); otherwise one of
      Medium or higher has an issue, and a lower one may be noted in the
      PR.
- [ ] Docs and the matching `.claude/rules/` files updated where
      behaviour, architecture or a rule changed
- [ ] A breaking public API change is noted in the PR, and a change to
      `AGENTS.md`, `docs/ARCHITECTURE.md` or `docs/TEST_STRATEGY.md` has a
      paragraph in the PR saying why
- [ ] No secrets, credentials or personal data
- [ ] PR title follows "PR title and commit hygiene"
- [ ] Manual testing decided and done per `docs/MANUAL_TESTING.md`
- [ ] Squash auto-merge enabled (see "Auto-merge")

## Choosing reviewers

Each review agent's focus is in `.claude/agents/<name>.md`. Select every
reviewer whose trigger the final diff touches; when unsure, include it.

| Agent | Selected when the diff touches |
|---|---|
| `docs-reviewer` | Docs, or behaviour, rules or commands that docs describe |
| `structure-reviewer` | Code, scenes or the solution layout |
| `concurrency-reviewer` | Threads, tasks, async flow, signals, events or node lifecycle |
| `errors-reviewer` | Failure paths: I/O, parsing, validation, exceptions, invalid state |
| `security-reviewer` | Input, storage, logs, permissions, credentials or personal data |
| `tests-reviewer` | Tests, or logic that needs them |
| `performance-reviewer` | Per-tick simulation paths, history generation or long-lived state |
| `build-reviewer` | CI, scripts, project, package or editor config, agent, rule or skill files, or hooks |

Node Runner's `ml-reviewer` and `design-lead`, and the skills and rules
for code this repository does not have yet, wait in `.claude/parked/`
until a change needs them; Claude Code and the arch tests ignore that
folder. Moving one back makes it follow this document.

## Findings format

`AgentFiles.FindingsFormat` in `EverlongDeep.Arch.Tests` holds the format,
with what each level means. Each code reviewer's `## Output` copies it
word for word, because agents do not reliably follow a link and fall
back on their own style. `AgentFilesSpec` fails when a copy differs.
`issue-reviewer` is no code reviewer and gives its own shape in its
`## Output`.

## Review receipts

Review runs on the change as it will be committed, measured from where
the branch left `main`: before the commit on the staged change, after it
with no uncommitted change to a tracked file. Untracked files stay out,
as they stay out of the commit, so a human's untracked work need not be
staged or moved.

When no new finding is left unaddressed, `.github/scripts/review-receipt.sh
write` records a receipt in `.git/review-receipts/`: the branch, base and
tree, the reviewers that ran, and each finding with its disposition. It
refuses while a tracked file has unstaged changes.

The receipt's key hashes the diff from the merge base with `main` or
`origin/main`, so committing the reviewed content, or rebasing it onto a
newer `main` without touching its files, keeps it valid. Any other
change needs a new receipt. After a simple, local fix, such as a rename,
a reworded line or a test the finding spelled out, the receipt needs no
new review; after a fix that changes behaviour or design, the reviewers
the fix selects run again. The receipt says which applied. An empty
change needs no receipt.

`guard-bash.sh` denies a `git push` of a branch unless a receipt matches
what the branch holds. A commit earlier in the same command is
measured, unless it names paths; after any other git command that
changes what the push sends, run `git push` as its own command. Tags,
deletes, dry runs and `release/vX.Y` branches pass.

## Agent, rule and skill files

Each kind of file holds one kind of content, and says nothing another
already says, except the copies "Findings format" names:

| File | Holds |
|---|---|
| `AGENTS.md` | Hard rules no script can check, and facts an agent would not guess |
| Docs | A topic, in the owner `README.md` → "Documentation" names, for the audience it names (below) |
| `.claude/rules/<topic>.md` | A layer's code rules, loaded when a file its `paths` match is read |
| `.claude/agents/<name>.md` | One agent's focus, inputs, work and output |
| `.claude/skills/<name>/SKILL.md` | A procedure's steps, naming the doc heading that owns each rule |
| Hooks | Rules on actions (see "Hooks") |
| Arch tests | Rules on file contents, in `EverlongDeep.Arch.Tests` |

A rule a script can check lives in a hook or an arch test, not only in
prose, and each denial or failure says what to do instead.

`README.md` → "Documentation" names each doc's audience. A doc for
agents is terse and addressed by heading. A doc for humans explains, and
links to the agent doc for a rule rather than restating it. A doc for
both is terse, with the why a human needs; split it only where the two
pull against each other, keeping one owner per topic.

`AgentFilesSpec`, `RuleFilesSpec`, `SkillFilesSpec` and `DocReferencesSpec`
in `EverlongDeep.Arch.Tests` check the rules below; no script checks the
last.

- Each agent is one `.claude/agents/<name>.md`. The folder holds nothing
  else but git-ignored `local_*` agents and dot-files.
- An agent's frontmatter has `name` (the file's `<name>`), a one-line
  `description`, `tools` and `model` (`sonnet`, `opus` or `haiku`), and
  may add `hooks`; nothing else. A value with `: ` or ` #` is invalid
  YAML unless quoted; prefer ` — `.
- A reviewer (`*-reviewer`) has `tools: Read, Grep, Glob, Bash, LSP` and
  registers `reviewer-bash.sh` and `reviewer-lsp.sh` in its frontmatter
  in Claude Code's nested `hooks` shape, as the existing reviewers do:
  Claude Code drops an agent whose hooks are flat.
- The `LSP` tool runs `csharp-ls`, pinned in `dotnet-tools.json`, from
  the `everlong-deep-csharp` plugin that `.claude/settings.json` enables
  from `.claude/marketplace/`; `LspConfigSpec` checks the chain.
- A rule in `.claude/rules/<topic>.md` has frontmatter with only `paths`:
  at least one quoted glob, one per line, each an existing file or
  `<folder>/**`.
- Each skill is `.claude/skills/<name>/SKILL.md`. Its frontmatter has
  only `name`, the folder's name in lowercase letters, digits and hyphens
  (a git-ignored `local_*` skill keeps its underscore),
  at most 64 characters, and a one-line `description` of at most 1024
  characters saying what it does and when to use it, since the model
  matches tasks against it. A skill the instructions name exists.
- A skill, agent or rule names each doc by an existing heading, as
  `` `docs/X.md` → "Heading" `` or `` `docs/X.md` §9 ``, so an agent
  reads only that section. A whole doc is named only as a topic's owner:
  `` `docs/X.md` owns … ``.
- Committed instructions require only the tools `README.md` → "Tools"
  marks as required, and name a fallback for a recommended one, such as
  `git grep` for `rg`.

## Hooks

`.claude/settings.json` registers the scripts in `.claude/hooks/` for
every agent; only `reviewer-bash.sh` and `reviewer-lsp.sh` are
registered by each reviewer's frontmatter. They hold what an
instruction alone does not reliably hold:

| Event | Script | Does |
| --- | --- | --- |
| `SessionStart` | `session-start.sh` | Tells the agent the branch and any uncommitted changes, records the running emulators, and runs `dotnet tool restore` for the `LSP` tool, waiting at most `EVERLONG_DEEP_RESTORE_WAIT` seconds (10 by default) |
| `PreToolUse` on `Bash` | `guard-bash.sh` | Denies the git commands `AGENTS.md` → "Working copy" forbids, a commit or push to `main`, a push without a review receipt, `gh pr create` without `--title`, and a PR title CI would reject |
| `PreToolUse` on `Bash`, in reviewers | `reviewer-bash.sh` | Allows only commands that read or build, such as read-only `git` and `gh`, `rg`, `dotnet build` and `dotnet test`; denies the rest and every redirect into a file |
| `PreToolUse` on `LSP`, in reviewers | `reviewer-lsp.sh` | Allows only lookups, so a reviewer stays read-only if the tool gains an operation that writes |
| `PreToolUse` on edits | `guard-edit.sh` | Denies writes into `/tmp` |
| `PostToolUse` on `Bash` | `after-push.sh` | After a `git push`, reminds the agent once of each emulator or `build/` APK that appeared during the session |
| `SessionEnd` | `session-end.sh` | Removes the session's state in `.git/agent-hooks/` |

The hooks guard against mistakes, not intent: `guard-bash.sh` reads
simple commands and allows what it cannot read, while `reviewer-bash.sh`
denies it.

A hook is a bash script that needs only bash, `git`, `jq` and POSIX
tools, plus a fractional `sleep`, which macOS and GNU accept (`session-start.sh` also runs `dotnet` when installed). It writes
its verdict in Claude Code's JSON shape and always exits 0, allowing the
call when `jq` is missing, because exit code 2 denies a `PreToolUse`
call and any other failure shows the human a hook error. The settings command is
`"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/<name>.sh`:
quoted for paths with spaces, and found from the repository root when
`CLAUDE_PROJECT_DIR` is unset. `guard-bash.sh` copies CI's PR title rule.
The specs in `EverlongDeep.Arch.Tests/Hooks/` run each hook against a
throwaway repository and check the registrations and the title rule.

## Merge gates

The `main` ruleset requires a PR, linear history, squash merges and the
checks `PR title`, `Build`, `Test & coverage` and `Format check`, with no
approval and no exceptions for administrators. Branches need not be up to
date with `main`, or auto-merge would stall whenever another PR lands
first; the CI run on `main` catches the rare semantic conflict.

## Auto-merge

Every PR enables squash auto-merge when it opens. The author does not
wait for the checks or the merge, but still owns the PR: when the next
task starts and before the session ends, check the PRs it opened and the
latest run on `main`, fix failing checks, and answer every review comment
with a fix or a reason; don't resolve threads you didn't address. If
auto-merge cannot be enabled, keep the PR open, report the blocker, and
never bypass the required checks.

## PR title and commit hygiene

- Format: `type(#123): Description`, for example
  `feat(#11): Add NPCs to the tavern`. A PR closing several
  issues lists them: `refactor(#497, #498): Description`.
- Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `build`,
  `ci`, `perf`, `style`, `revert`.
- The description starts with an uppercase letter.
- The squash commit takes the PR title as its subject, so commits inside
  a PR may be messy.
- Add a `Co-authored-by:` trailer for every human or agent that
  contributed materially.
