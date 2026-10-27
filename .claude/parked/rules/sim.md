---
paths:
  - "project/src/sim/**"
---

# Sim rules — `project/src/sim/`

The training loop: trials, slots and generations. `docs/TRAINING_LOOP.md`
owns how it behaves, including the one population and the collision
layers.

## Rules

1. **Raise C# events that hosts adapt;** never reach UI, view-models or
   managers.
2. **A trial begins before any creature drives that tick,** however it is
   started, so the same network replays the same run:
   - `TrialController.ProcessPhysicsPriority = -100`, below `Creature`'s
     0, so the new network drives the trial's first step.
   - `StartTrial` begins at once inside a physics tick
     (`Engine.IsInPhysicsFrame`) and otherwise at the start of the next
     tick.
   - `Creature.ResetPose` builds every body and joint afresh, because
     Godot's joints and contacts remember their last push and would apply
     it again.
3. **Logic that needs no scene goes in `libs/`,** where it is tested; the
   GA is in `libs/EverlongDeep.ML`.
