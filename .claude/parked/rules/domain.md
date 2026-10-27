---
paths:
  - "libs/EverlongDeep.Domain/**"
  - "tests/EverlongDeep.Domain.Tests/**"
---

# Domain rules — `libs/EverlongDeep.Domain`

The app's vocabulary as plain data, with no behaviour beyond invariants.

## Rules

- **No I/O:** no `System.IO`, `System.Net` or environment access.
- **Records, enums and constructor invariants only.** The constructor
  validates, so bad data never survives construction; logic lives in the
  layers above, and part physics in `libs/EverlongDeep.Mechanics`.
- **Build a `CreatureDef` from one list of parts.** Its per-kind
  constructors are `internal`, for tests and JSON only, so a new part kind
  changes no production caller.
- **Serialisable with `System.Text.Json` without custom converters.** Saved
  records are the save shape: change one with the `change-saved-shape`
  skill.

### Exceptions: helpers kept here

Pure, stateless static classes over Domain types, or immutable values
computed from them, kept because a layer that cannot use Mechanics needs
them (`docs/ARCHITECTURE.md` → "Dependency rules"). This is the one
list; add or move one only with an issue.

| Type | Why it is here |
|---|---|
| `PartKinds`, `PartKindInfo` | Each part kind once (selector, sensor variant, joint slot, ports), so `CreatureDef` and `NetworkPorts` walk the kinds instead of listing them |
| `NetworkPorts` | Every part's ports, their order and channel keys, shared by the sim, Build and ML |
| `PortSignals` | Each output's activation and passive start: the network's half of the port contract |
| `SensorDef.DefaultAim` | A new Camera's aim, filled in by `CreatureDef` |
| `SensorPicture` | Where a sensor's picture sits and senses from, and what a tap on it hits |
| `SelectionMarks` | The selection gap, a joint's halo and touch reach, shared by Build's touches and the drawing |
| `AngleSensorDef.ArcRadius`, `ArcRadiusAt`, `MarkRadius`, `HaloRadius` | Where an Angle sensor's arc and halo sit, so drawing and Build's rings agree |
| `MapGround` | A map's ground height at x, read by the arena |
| `DrawGroups`, `DrawSlot`, `RaisedParts` | The draw order and raised parts, so drawing and touches in Build and Training rank parts alike |
