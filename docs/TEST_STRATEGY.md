# Test Strategy

How each layer of Everlong Deep is tested, with what tools, and why.
Test-code conventions are in `.claude/rules/tests.md`.

## Guiding principles

1. **A few tests that cover a lot.** Drive a behaviour through its public
   entry with a realistic input, and assert what the user or the next
   layer relies on. A test that can only fail when another does is
   merged or deleted.
2. **Test the risky bits, not the trivia.** New simulation logic has
   unit tests; branching, parsing, numerics and state changes are pinned
   down, not coverage percentages.
3. **Fast.** `dotnet test EverlongDeep.slnx` finishes in seconds; anything
   slow is a smell.
4. **Deterministic,** by `docs/CODE_DESIGN_PRINCIPLES.md` §4.
5. **Rules that can be checked are code.** Layer and file rules live in
   `EverlongDeep.Arch.Tests`. Add a test when we decide to enforce a
   convention.
6. **A world is a test fixture.** The simulation runs headless with
   event injection and logs (`docs/01 Simulation Architecture.md`), so a
   behaviour is tested by building a small world, injecting events and
   asserting on what people believe and do.

## Layers

| Layer | Lives in | Tooling | What's tested |
|---|---|---|---|
| Simulation | `libs/EverlongDeep.Simulation/` | xUnit + Shouldly | People, relations, rumors, wants: seeded scenarios in small worlds |
| CLI | `tools/EverlongDeep.Cli/` | xUnit + Shouldly | Argument parsing; a seed reproduces its output |
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
- An LLM's wording: test what the simulation hands it and does with its
  reply, not the prose
- Code shape (sealed/abstract, member kinds, base types): test the values
  a contract maps to, or guard against the hardcoding it prevents

## Running the suite

```bash
dotnet test EverlongDeep.slnx                                         # all tests
dotnet test tests/EverlongDeep.Simulation.Tests                       # one project
dotnet test EverlongDeep.slnx --collect:"XPlat Code Coverage"         # + coverage
```

CI runs the coverage form and uploads the report.
