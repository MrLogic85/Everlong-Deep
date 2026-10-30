# Code Design Principles

The implementation rules we hold ourselves to, including how we write
comments and docs.

## 1. Clarity beats cleverness

The simulation is the game: a reader must be able to follow why an NPC
believes, wants or does something.

- Prefer a straightforward loop over a chained LINQ one-liner.
- Name variables for meaning (`confidence`, `travelDays`), not brevity.
- A formula that weighs beliefs, relations or wants gets a one-line
  comment saying **what** it models, and **why** it is shaped that way.
- Do not optimize until a profiler says you must.

## 2. Godot first

Using Godot to its full potential is the default, not an option.

- Before writing anything, find what Godot already provides (nodes,
  resources, servers, project settings, editor features) and use it
  fully, even if that means learning a new part of the engine, such as
  `RigidBody2D` and joints, theme type variations, or `TranslationServer`.
- Write our own only when Godot cannot do what we need, and say why in
  the code or the owning doc, naming what was checked. "Ours is simpler"
  or "we didn't know" is not a reason.
- A wrapper that only renames or re-routes a Godot feature is still our
  own solution. Extend the Godot type (subclass, theme variation,
  resource) instead.
- The engine-independent `libs/` (§3) cannot call Godot, but that does
  not license re-implementing a Godot feature there: hand the Godot side
  the data it needs and let Godot do the work. Logic Godot does not
  offer, such as the world simulation, stays ours.
- Existing code that duplicates a Godot feature gets an issue to replace
  it.

## 3. The simulation is engine-agnostic

Nothing in `libs/` references Godot (`docs/ARCHITECTURE.md`); the arch
tests enforce it. The world has to run where no game is running: in
tests, on the CLI, and while generating history before the player
arrives (`docs/01 Simulation Architecture.md`).

## 4. Determinism by default

A run reproduces from its seed and what was injected into it, so bugs,
regressions and balance changes are detectable in a test.

- All randomness flows through one seeded `System.Random` per run, passed
  to whoever needs it; never an ad-hoc `new Random()`. A seeded
  `System.Random` gives the same sequence on every .NET platform, so no
  custom PRNG is needed.
- The log holds the run's seed and each injection; a scripted event
  is not logged yet (#100).
- Simulation time is a value the simulation advances, never the wall
  clock: no `DateTime.Now` decisions in `libs/`.
- An LLM reply never decides inside a step, and becomes state only
  through an injection (`docs/ARCHITECTURE.md` → "Threading").
- An injection's record holds its tick and the call with its arguments.
  A replay makes each recorded call again at its tick, in the log's
  order and without the LLM, and gives the same log. A scripted event's
  record can be its scenario's name, since the scenario rebuilds the
  event from the world.

## 5. Small, composable units

- A file does one thing. Aim for about 300 lines; past that, check
  whether it still does one thing.
- **Soft limit, 1000 lines.** A production file past it is split, or the
  change says why it stays whole.
- **Hard limit, 2000 lines** for a `.cs` file under `libs/` or
  `project/src/`, once the game has one; `EverlongDeep.Arch.Tests` enforces it. Test files have no
  limit.
- Prefer composition over inheritance: an NPC *has* traits, relations,
  beliefs and wants.
- No abstract base class until the second concrete case appears.

## 6. Data before behavior

The world is records first, such as people, places, beliefs and
relations, which makes saving, diffing, logging and testing trivial.
Godot nodes are built *from* this data; they don't replace it.

## 7. Explicit units

- Simulation time: one unit, documented once where the simulation
  defines it, and named in identifiers (`travelDays`).
- Angles: **radians**, converted at the UI boundary. Never a bare
  `double angle`; call it `angleRad`.

## 8. Fail loud in dev, gracefully in prod

- Use `Debug.Assert` liberally for invariants
  (`Debug.Assert(weights.Length == expected)`).
- Release builds clamp or log rather than crash.
- Never `catch (Exception) { }`.

## 9. Comments and docs

Comment intent, not mechanics. Assume the reader knows C#, not why
*this* algorithm cares about *that* invariant:

```csharp
// BAD: increments the counter
generation++;

// GOOD: we count generations from 1 so the UI matches user intuition
generation++;
```

- Prefer clearer code, such as a better name or a smaller method, over a
  comment that explains it.
- Say what something is for, not where it is used; callers change.
- Describe what is true now and why. No history ("previously", "no
  longer", "after the refactor"), and no issue numbers except a link to
  an open issue for pending work. Pending work itself, such as TODOs and
  wished-for improvements, goes in a GitHub issue.
- Doc comments (`///`) go only on public API and subtle internals, with
  `<param>` and `<returns>` only where they add to the summary.

Ask four questions of every text you write, change or make wrong:

1. **What does it add; is it needed?** Delete text no reader acts on:
   generic advice, a restatement without a why of what code, tests or CI
   enforce, and history that no longer guides a decision. A change
   removes what it makes unneeded, not only adds.
2. **Is it right and clear?** One clear sentence beats two.
3. **Is it said somewhere else?** Nothing is written twice; other files
   link to the owner, which `README.md` → "Documentation" names.
4. **Is it in the right place?** Each file has one responsibility, as
   `docs/REVIEW.md` → "Agent, rule and skill files" lists.

## 10. Testing

`docs/TEST_STRATEGY.md`.

## 11. Version control

`docs/REVIEW.md` → "PR title and commit hygiene".

## 12. Dependencies are a debt

Every NuGet package or Godot addon is future maintenance, and Godot (§2)
beats a package, an addon or our own version.

- The simulation's rules are ours, so they can be read, tested and
  balanced.
- An LLM client is a dependency like any other: behind an interface the
  Storyteller owns, so tests and history generation run without it. A
  client that needs a package lives in the game, never in the
  Storyteller or the CLI (`docs/ARCHITECTURE.md` → "Dependency rules").

## 13. UI is the last mile

- Never let a beautiful UI hide a broken simulation: sim first, UI on
  top.
- The game reads its state from the simulation, and reports what the
  player did back as events; the simulation doesn't know the game
  exists.

---

## Style specifics (C#)

`.editorconfig` and `dotnet format` own formatting and naming. Beyond
them:

- `readonly` everywhere it fits
- `sealed` by default on classes, unsealed only when subclassing is
  planned
- `record` for immutable data, `class` for identity and state
- No `#region`; a file that needs regions is too big

## Anti-patterns we explicitly reject

- Singletons that hold mutable game state (autoloads for pure services
  are fine)
- Reflection-based "magic" wiring
- God objects (`GameManager` that knows everything)
- Deep inheritance chains
- Async/await where a per-frame update would do
- Premature ECS / DOTS-style architecture

## Chosen tooling

Changing a choice needs an issue. Test tools are in
`docs/TEST_STRATEGY.md` → "Tools"; target frameworks in
`docs/ARCHITECTURE.md`.

| Concern | Choice | Why |
|---|---|---|
| Solution format | **`.slnx`** (XML) | Readable diffs, no GUIDs |
| Package versioning | **Central Package Management** (`Directory.Packages.props`) | One source of truth for versions; the Godot csproj opts out, since its SDK manages its own |
| Code style | **`.editorconfig` + `dotnet format`** | Editor-agnostic |
| DI | **Hand-rolled constructor injection**, autoloads as composition root | |
| CLI tables | **Spectre.Console**, the CLI's one package | Colored tables that fit the terminal, so a conversation trace reads at a glance |

- `TreatWarningsAsErrors` is on in `Directory.Build.props`; a Godot
  project, when one is added, turns it off because Godot's source
  generators emit code we don't own.
- `GenerateDocumentationFile` is on so `IDE0005` (unused usings) fails
  the build; `NoWarn` suppresses the `CS1591` (missing XML docs) it
  triggers.
