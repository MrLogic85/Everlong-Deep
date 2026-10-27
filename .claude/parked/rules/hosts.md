---
paths:
  - "project/src/hosts/**"
  - "project/scenes/hosts/**"
---

# Host rules — `project/src/hosts/`

Routed scene roots that wire screens to managers and the navigator
(`docs/ARCHITECTURE.md` → "Dependency rules" and "Navigation").

## Rules

1. **One `*Host` per routed screen.** The script is the root of
   `project/scenes/hosts/<Name>Host.tscn`, a full-rect Control that
   ignores the mouse (`.claude/rules/ui-lib.md` → "Theme and tokens"),
   and instances its screen from `project/scenes/screens/`. Helpers only
   hosts use live here too, and the `SettingsApplier` autoload.
2. **Pause is global.** `TrainingHost` resets `GetTree().Paused` in `_Ready`
   and `_ExitTree`, and its root is `ProcessMode.Always` so the screen
   keeps responding while paused; the creature, `Evolver` and each slot are
   pinned to `Pausable` so training freezes.
3. **A host run on its own (F6) has no route** (`_route is null`) and
   still opens: `TrainingHost` trains the Walker example without saving.
