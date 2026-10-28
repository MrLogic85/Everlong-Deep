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
`--trace`, each conversation is followed by tables: both people as they were,
every choice with its chance and roll, the story as held and as told, and what
changed. In a terminal they are colored; redirected to a file, they are not.

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
