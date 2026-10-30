# Test Strategy

How each layer of Everlong Deep is tested, with what tools, and why.
Test-code conventions are in `.claude/rules/tests.md`.

## Guiding principles

1. **A few tests that cover a lot.** Drive a behaviour through its public
   entry with a realistic input, and assert what the user or the next
   layer relies on. A test that can only fail when another does is
   merged or deleted.
2. **Lock behaviour in at a stop, not before.** A rule is tuned until
   the build-order stop that covers it is reached
   (`docs/01 Simulation Architecture.md` → "Build order"). Before that
   stop, tests cover only what tuning doesn't change: determinism
   (`docs/CODE_DESIGN_PRINCIPLES.md` §4),
   logic whose expected result stays when the rule is tuned (a clamp, a
   boundary), and that tools parse their arguments. At the stop, scenario
   tests lock in the behaviour the owner agreed. No test ever asserts
   log or trace wording, or, before the stop, a number tuning would move.
3. **Asserts are the invariant tests.** Tests run in Debug, in CI too,
   and a seeded run over many seeds and hours reaches the code's asserts
   (`docs/CODE_DESIGN_PRINCIPLES.md` §8).
4. **Fast.** `dotnet test EverlongDeep.slnx` finishes in seconds; anything
   slow is a smell.
5. **Deterministic,** by `docs/CODE_DESIGN_PRINCIPLES.md` §4.
6. **Rules that can be checked are code.** Layer and file rules live in
   `EverlongDeep.Arch.Tests`. Add a test when we decide to enforce a
   convention.
7. **A world is a test fixture.** The simulation runs headless with
   event injection and logs (`docs/01 Simulation Architecture.md`), so a
   behaviour locked in at a stop is tested by building a small world,
   injecting events and asserting on what people believe and do.

## Layers

| Layer | Lives in | Tooling | What's tested |
|---|---|---|---|
| Simulation | `libs/EverlongDeep.Simulation/` | xUnit + Shouldly | Determinism, logic tuning doesn't change, and a seeded run that reaches the asserts; at a stop, seeded scenarios in small worlds |
| Storyteller | `libs/EverlongDeep.Storyteller/` | xUnit + Shouldly | What it injects into the simulation; never its wording |
| CLI | `tools/EverlongDeep.Cli/` | xUnit + Shouldly | Argument parsing and its failures |
| Architecture | `libs/`, `tools/`, agent files, LSP config, hooks | xUnit + Shouldly | Layer graph, file size, agent, rule and skill files, hooks |
| Balance | seeded simulation runs | Manual (`docs/MANUAL_TESTING.md`), with checks promoted to tests once agreed | Whether the world behaves believably |

## Tools

Changing a tool needs an issue; `Directory.Packages.props` pins the
versions.

| Tool | Purpose | Why |
|---|---|---|
| xUnit | Test framework | Standard, mature, fast |
| Shouldly | Assertions | Apache 2.0; FluentAssertions changed licence at v8 |
| coverlet | Coverage | The `dotnet test` default |

## What we do NOT test

- Compiler-generated record members (`Equals`, `GetHashCode`, `ToString`)
- Trivial getters and setters, or setting a value and reading it back
  through code with no logic
- .NET BCL or Godot engine behaviour
- The Storyteller's wording, an LLM's included: test what it injects into
  the simulation, not the prose
- Code shape (sealed/abstract, member kinds, base types): test the values
  a contract maps to, or guard against the hardcoding it prevents

## Running the suite

```bash
dotnet test EverlongDeep.slnx                                         # all tests
dotnet test tests/EverlongDeep.Simulation.Tests                       # one project
dotnet test EverlongDeep.slnx --collect:"XPlat Code Coverage"         # + coverage
```

CI runs the coverage form and uploads the report.
