---
paths:
  - "libs/EverlongDeep.Mechanics/**"
  - "tests/EverlongDeep.Mechanics.Tests/**"
---

# Mechanics rules — `libs/EverlongDeep.Mechanics`

The pure physics of creature parts: the sums the sim runs each step, which
Build and tests reuse so they show and check exactly the same thing.

## Rules

- **Pure and stateless.** Static methods take values and return values.
  State that carries between steps (a `ProofMass`) is a
  `readonly record struct` the caller keeps and passes back. No I/O, global
  state or randomness.
- **Only what Godot lacks:** a part's own rule, never a rigid-body solver
  (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
- **The network contract stays in Domain** (port order, channel keys,
  activations; `.claude/rules/domain.md`). This library turns a port's
  value into physics (`OutputSignals`) and physics into a reading.
