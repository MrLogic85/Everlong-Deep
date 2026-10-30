# AGENTS.md

Guidance for AI coding agents and human contributors working in the
**Everlong Deep** repository.

## What this project is

Everlong Deep is a **single-player open-world pirate sandbox** where the
world runs on beliefs, not facts: NPCs act on what they believe, and
stories about the player spread, mutate and arrive late
(`docs/00 Overview.md`).

- C# for everything; versions in `README.md` → "Tools".
- The world simulation comes first, as a headless library with no
  engine (`docs/01 Simulation Architecture.md`); a Godot game is built on
  top of it later.
- The build order and its stop points in
  `docs/01 Simulation Architecture.md` → "Build order" decide what to
  work on: nothing new is added past a stop point until what is there
  works or is balanced.

Read the documents your task touches, not all of `docs/`: `README.md` →
"Documentation" names the owner of each topic.

## How to act

This is how I'd like us to talk to each other.

When I ask a question or think aloud, I want a conversation, not a pull
request. Answer me, tell me what you think and propose what you'd do.
Start building when I ask for it or agree. If we land on something worth
keeping, record it in an issue; you decide when that is.

Be honest rather than agreeable. If you think I'm wrong, say so and tell
me why. If something didn't work, say that plainly: "I couldn't get this
to work" is a useful answer. A claim that something works needs
evidence, and praise needs something to point to.

The rules in this repository have reasons. When a case doesn't fit a
rule, tell me why and wait for my answer before you depart from it.

Talk to me in my language.

## Prime directives

1. **Follow the owning documents and keep them current and short.** Write
   docs with the `write-docs` skill, and agents, skills, rules files,
   hooks and this file with the `write-agent-files` skill.
2. **Godot first, always** (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
3. **Run the review agents before every commit or push** with the
   `review-change` skill, then ship with the `ship-pr` skill.
   `docs/REVIEW.md` → "Definition of Done" owns the gate.
4. **Work autonomously.** Pause only when blocked by hardware or when a
   decision needs a human. Pick work as "How to act" says, or Ready
   issues on GitHub, and start or resume each issue with the
   `start-issue` skill.
5. **Record work and decisions on GitHub.** File bugs, improvements and
   decisions as issues with the `create-issue` skill, and review broad,
   risky or ambiguous issues with the `review-issue` skill. Suggest large
   refactors when they would benefit the code.
6. **Keep code and project terms in English,** whatever language you
   talk in: write identifiers, concept names (as in
   the design docs), file names and quoted issue text exactly as they
   are; translated, they are hard to recognise.

## Working copy

- Work only in the checkout the session started in. Do not create git
  worktrees, extra clones or copies of the repository.
- Do each change on a feature branch (`git switch -c ...`), and switch
  back to `main` once its PR is open. `main` changes only through a merged
  PR (`docs/REVIEW.md` → "Merge gates").
- If the checkout has uncommitted changes or is on another branch when
  work should start, stop and ask the human. Never stash, reset or discard
  them, and never work around them.
- Scratch files (screenshots, builds, renders, notes) go in the
  git-ignored `.scratch/`, never in `/tmp`; delete them when no longer
  needed.

## Repository map

- `libs/`: the engine-independent simulation, and the Storyteller that
  words what happens in it.
- `tests/`: tests of the simulation, the architecture and the agent files.
- `docs/`: the numbered game design documents, and the process documents.
- `.claude/parked/`: Node Runner's agents, skills and rules for code this
  repository does not have yet (`docs/REVIEW.md` → "Choosing reviewers").

`docs/ARCHITECTURE.md` owns the layers, and
`docs/CODE_DESIGN_PRINCIPLES.md` §9 where rules and procedures live. Claude
Code loads a `.claude/rules/` file when you read a file its `paths`
match; read the matching rules yourself before creating a file there, or
when you work from a diff alone. When a directive names a skill, use it
even if it did not load by itself.

## Machine-local setup

`LOCAL_CONFIG.md` (git-ignored, one per clone) holds host-specific state:
GitHub CLI accounts, SSH host aliases and paths. Read it before running
`gh`, `git push` or anything that touches an external account; the
primary `gh` login may not be the one with write access to this repo.

Skills, agents, rules and hook scripts named `local_*`, and
`.claude/settings.local.json`, which registers local hooks, are
git-ignored too. They hold one machine's procedures, such as a test
device's lock; committed instructions never require one to exist.
