# Architecture

Where code lives and which way it may depend. Why the simulation is a
headless library, and what it owns versus the game, is in
`docs/01 Simulation Architecture.md`.

## Layers

`libs/EverlongDeep.Simulation/` is a .NET 8 class library with **no
`Godot.*` references**: the world runs in tests, on the CLI and during
history generation without an engine. A Godot project, when the game is
built on top, will reference it and own physical space, scenes and the
player's actions.

`tests/EverlongDeep.Arch.Tests/ArchitectureSpec.cs` enforces the
"Dependency rules" table.

## Dependency rules

| Layer | Where | Depends on | Never on |
|---|---|---|---|
| **Simulation** | `libs/EverlongDeep.Simulation/` | the .NET BCL | Godot, any LLM client |
| **CLI** | `tools/EverlongDeep.Cli/` | Simulation, the .NET BCL | Godot, packages, any LLM client |

- An LLM narrates behind an interface the simulation owns
  (`docs/CODE_DESIGN_PRINCIPLES.md` §12), so the simulation builds and
  tests without one.
- Split a layer out of Simulation only when a second consumer or a
  boundary the tests should enforce appears, and add it to this table.

## Threading

The simulation is single-threaded: a step runs to completion before the
next starts, which keeps seeded runs reproducible
(`docs/CODE_DESIGN_PRINCIPLES.md` §4). Work moved to another thread, such
as an LLM call, hands its result back to be applied at a step boundary.

## Solution layout

```
EverlongDeep/
├── EverlongDeep.slnx               # solution (XML format)
├── Directory.Build.props           # shared compiler settings
├── Directory.Packages.props        # central package versions (CPM)
├── dotnet-tools.json               # local .NET tools: csharp-ls for the agents' LSP tool
├── libs/
│   └── EverlongDeep.Simulation/    # the headless world simulation (net8.0)
├── tests/
│   ├── EverlongDeep.Arch.Tests/    # layer, file and agent-file rules, hook specs (net10.0)
│   ├── EverlongDeep.Cli.Tests/     # argument parsing and seed reproducibility (net10.0)
│   └── EverlongDeep.Simulation.Tests/
├── tools/
│   └── EverlongDeep.Cli/           # runs a seeded world and prints its log (net10.0)
└── docs/                           # game design and process documents
```
