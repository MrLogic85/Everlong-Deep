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

The simulation writes no text of its own. Text the Storyteller's LLM
writes, such as a story's details, is already worded, so the simulation
will carry it as written
([#56](https://github.com/MrLogic85/Everlong-Deep/issues/56)). Each
thing that happens goes out through its log as a record, with the values
as they were then, to the listener the log was made with, in the order
it happened; the simulation keeps none of them.
`libs/EverlongDeep.Storyteller/`, also .NET 8 so the game can reference
it, listens and words each record for the game and the CLI alike,
keeping what they need. The CLI prints what the Storyteller
hands it, beside the CLI's own debugging tables.

`tests/EverlongDeep.Arch.Tests/ArchitectureSpec.cs` enforces the
"Dependency rules" table, that no log record holds a string, and that
the simulation keeps no collection of records.

## Dependency rules

| Layer | Where | Depends on | Never on |
|---|---|---|---|
| **Simulation** | `libs/EverlongDeep.Simulation/` | the .NET BCL | Godot, the Storyteller, any LLM client package |
| **Storyteller** | `libs/EverlongDeep.Storyteller/` | Simulation, the .NET BCL | Godot, Spectre.Console, any LLM client package |
| **CLI** | `tools/EverlongDeep.Cli/` | Simulation, Storyteller, the .NET BCL, Spectre.Console | Godot, other packages, any LLM client package |

- The LLM sits behind an interface the Storyteller owns
  (`docs/CODE_DESIGN_PRINCIPLES.md` §12), so the Storyteller builds and
  tests without one.
- Split a layer out of Simulation only when a second consumer or a
  boundary the tests should enforce appears, and add it to this table.

## Threading

A step runs to completion before the next starts, which keeps seeded
runs reproducible (`docs/CODE_DESIGN_PRINCIPLES.md` §4); how the
simulation uses threads is not decided yet. Work moved to another
thread, such as an LLM call, hands its result back to the Storyteller.
The Storyteller injects what follows from it through the simulation's
public API at a step boundary, never by touching its state, and records
it as `docs/CODE_DESIGN_PRINCIPLES.md` §4 says.

## Solution layout

```
EverlongDeep/
├── EverlongDeep.slnx               # solution (XML format)
├── Directory.Build.props           # shared compiler settings
├── Directory.Packages.props        # central package versions (CPM)
├── dotnet-tools.json               # local .NET tools: csharp-ls for the agents' LSP tool
├── libs/
│   ├── EverlongDeep.Simulation/    # the headless world simulation (net8.0)
│   └── EverlongDeep.Storyteller/   # words the simulation's records (net8.0)
├── tests/
│   ├── EverlongDeep.Arch.Tests/    # layer, file and agent-file rules, hook specs (net10.0)
│   ├── EverlongDeep.Cli.Tests/     # argument parsing (net10.0)
│   └── EverlongDeep.Simulation.Tests/
├── tools/
│   └── EverlongDeep.Cli/           # runs a seeded world and prints its log (net10.0)
└── docs/                           # game design and process documents
```
