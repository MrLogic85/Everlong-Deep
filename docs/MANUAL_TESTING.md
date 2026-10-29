# Manual testing

Manual testing is decided per change; add no ritual checks just because a
PR exists.

## Decision point

Every issue states whether manual testing is expected. If it is silent,
the PR author decides before opening the PR and records the decision in
**How to verify**.

Manual testing is usually required when the change affects:

- How believable the world's behaviour and stories are: rumors, wants,
  relations, factions
- Balance, which tests pin but a human judges
- Save/load behaviour that a user can observe
- Godot scenes, input, rendering or UI, once the game has them
- Anything where "it compiles" does not prove the player's experience

It is usually not required for documentation, pure-C# logic covered by
tests, build or CI metadata proven by its check, or issue-tracker
bookkeeping. When in doubt, write one small manual check in the issue.

## Running a seeded world

`dotnet run --project tools/EverlongDeep.Cli -- --seed 42` builds a
seeded world, runs it and prints its log; an unknown argument prints its options. With
`--trace`, a legend of the number columns' scales prints once. Each
conversation then opens with a rule naming who told whom and the outcome, and
these tables follow its log line:

- **Who and what**: the two people, the told story's people, things and place,
  and everyone the listed beliefs show, with their role.
- **Feelings**: how each of the two feels about the other and about the people
  the story names.
- **Beliefs (before)**: both people's beliefs that name either of them or
  someone or something the story names, or that they have heard from or told
  each other.
- **Choices**: every choice with its chance and roll.
- **Story**: the story as held and as told.
- **Changes**: what changed.

A claim names its actor, target and witness; a place alone doesn't count.

A person or thing is written by id and name, `p1 Bram`, since names can repeat.
In a terminal the tables are colored and as wide as their content; redirected
to a file, they are plain and wide enough never to wrap a cell.

## What belongs in an issue

Manual checks live with the issue they prove, in this format:

```markdown
## Manual test plan

Environment:
- Seed and settings:

Steps:
1. ...

Expected:
- ...

Evidence:
- Log excerpt, screenshot or recording:
```

## What belongs in a PR

The PR's **How to verify** section records what actually ran:

- Automated commands and their results
- Manual checks performed, and those skipped with a reason
- The seed and settings of every simulation run
- Log excerpts, screenshots or recordings the judgement rests on

## Agent-run manual tests

"Manual" describes the user-visible workflow, not who runs it. An agent
may run a check whose result it can observe with its tools, and records
the evidence it used before claiming a pass. A check that needs
subjective judgement, such as whether a story feels earned, or whose
runtime is missing, is left to the human with the reason; an agent never
reports it as passed. The `plan-manual-tests` skill writes the plan.

## Simulation checks

While the game is a headless simulation (`docs/ARCHITECTURE.md`), a
manual check is a seeded run, judged from its log: whether what happened
is believable, not only whether it is correct. Record the seed, the
settings and the log excerpt the judgement rests on.
