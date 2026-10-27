---
paths:
  - "project/src/creature/**"
  - "project/scenes/Creature.tscn"
  - "project/project.godot"
---

# Creature rules — `project/src/creature/`

How Godot realises a creature: bodies, joints, sensors, driven parts and
the network wiring. `docs/CREATURE_MODEL.md` owns the model. Code cites
these rules by number, so keep the numbering.

## Rules

1. **Built from `CreatureDef`.** The def is the source of truth; no
   `[Export]` for anything a def sets.
2. **No game logic:** no evolution, fitness or UI. Given a def and a
   network, the creature reacts to forces and drives its parts.
3. **Bodies and joints.** Each node and each beam is its own `RigidBody2D`
   (`CanSleep = false`), and a `PinJoint2D` pins every beam to its two
   nodes, so beams are rigid by geometry, not springs.
   - `project.godot` keeps `physics/2d/solver/solver_iterations` at 24:
     fewer lets beams stretch and rigid triangles turn inside out, and each
     step up costs tick time on a phone.
   - While a beam is intact only nodes collide; beams and links have no
     collider, as a shape on every beam of many shadows nearly triples a
     phone's tick. A torn beam gets one (rule 17).
   - No body uses continuous collision detection: the ground is an endless
     half-plane a node cannot tunnel through, and Godot's ray CCD sets a
     fast body's velocity outright, which launches rolling creatures.
     Revisit when grounds get shapes
     ([#91](https://github.com/MrLogic85/Everlong-Deep/issues/91)).
   - Bodies set no damping of their own, so only the project's physics
     defaults slow them (`docs/CREATURE_MODEL.md` → "Air drag"); a Wheel's
     spin is the one exception.
4. **End stops.** Each Piston and Spring has a hidden, collider-free
   cylinder body pinned to node A and a `GrooveJoint2D` to node B that holds
   its length between the stops. Anything that moves or resets every body
   includes the cylinders. Each groove's force is capped at
   `Mechanics.EndStop.MaxForce`: Godot's solver cannot settle stops that
   close a loop with beams, and an uncapped stop stores a push that throws
   the creature into the air.
5. **Springs.** A Spring is a `DampedSpringJoint2D` between its nodes, with
   the end stops of rule 4. Each tick `SpringLink` sets its rest length to
   `Spring.StepRestLength`, because Godot's spring would push a preload past
   a stop and break the stop's joints.
6. **Hinge parts reach their sides through `HingePartSides`.** A link side
   gets equal-and-opposite force couples on the joint body and the link's
   far body; a Wheel side gets a plain torque on the Wheel body. No Godot
   angular joint: Pistons and Springs slide on a groove, and couples work
   for every link kind without branches. The lever arm is clamped to
   `HingePartSideGeometry.MinimumLeverArm`, so a compressed Spring cannot
   make unbounded force. Torques are integrated explicitly, so gains scale
   with the live inertia to stay stable.
7. **Accelerometer.** `AccelerometerSensor` turns the velocity change where
   its picture sits into specific force and steps the Mechanics
   `Accelerometer`, which substeps so Build's longer frames stay stable.
8. **Network order is `NetworkPorts.Of`.** Copy each part's value to its
   port's place; never hand-order network slots here.
9. **No allocations in the tick.** Reuse the sensor, output and scratch
   buffers; keep `_PhysicsProcess` a short sequence of named steps.
10. **Every visual declares its shadow drawing** through `IShadowVisual`.
    Shadows are drawn in the `ArenaShadows` viewport, chosen by
    `ArenaVisibility` layers, because a `CanvasGroup` lets the parts' own
    draw layers escape. The look is `docs/WORLD_VISUALS.md` → "Drawing as a
    shadow".
11. **A Wheel is its joint's body.** Unlock that body's rotation and give
    it the Wheel's weight and inertia; add no body or joint.
12. **A Touch sensor is a `ShapeCast2D` on its joint's body,** casting the
    joint's cushion to find the nearest ground in any direction, since a
    contact monitor only says on or off and the reading is analog. Add no
    area or body.
13. **A Rubber is its joint body's material:** a Rough `PhysicsMaterial`
    at `Rubber.Friction`, with no body, shape or collider of its own.
14. **A Brake is friction on its `HingePartSides`, or a weld when Locked.**
    A `BrakeJoint` applies `Mechanics.Brake`'s torque every tick, network
    or not, using `HingePartSides.BrakeInertia`, which must never read
    heavier than the joint. A Locked Brake is a `HingeWeld` instead, as no
    Godot angular joint can brake; `Creature.Links.cs` (Welder) says when
    it stays friction.
15. **An Angle sensor only reads its `HingePartSides`;** it never applies
    torque or adds a joint.
16. **A Stepper steps by torque and holds by a weld.** A `StepperJoint`
    runs `Mechanics.Stepper` every tick, network or not. While stepping it
    turns its sides by torque (rule 6); while locked it welds them at the
    notch with one `HingeWeld` made with the creature and switched on and
    off, so the tick makes no node (rule 9).
17. **A tear clears a pin, it frees nothing.** A torn end sets its
    `PinJoint2D`'s `NodeA`/`NodeB` to an empty path: the pin stops with no
    kick, and every body and joint keeps its slot, so nothing points at a
    freed object. A torn end gets a circle collider as wide as the beam, so
    it lands instead of passing through the ground; a beam gone at both
    ends turns that shape into a capsule and falls, never hidden or
    disabled. A `HingeWeld` is released the same way. What tears is
    `docs/CREATURE_MODEL.md` → "Beam" (Tearing).
18. **Read bodies once a tick.** `ReadBodies` keeps every beam's transform
    and node's position for the physics frame, so the tear check and the
    measures share one read; `Build` and `ResetPose` clear it.

Pure sums belong in `libs/EverlongDeep.Mechanics`, where they are tested;
`docs/MANUAL_TESTING.md` → "Decision point" decides when a physics
change needs a manual check.
