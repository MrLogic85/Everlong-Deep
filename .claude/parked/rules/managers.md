---
paths:
  - "project/src/managers/**"
  - "project/project.godot"
---

# Manager rules — `project/src/managers/`

App-wide services as Godot autoloads, registered in `project/project.godot`
→ `[autoload]`. They are the composition root (`docs/ARCHITECTURE.md` →
"Layers").

## Rules

1. **No UI code.** No `Control` references; only `SceneRouter` changes
   scenes. UI autoloads live in `project/src/ui/lib/`.
2. **Hand out App interfaces, not concrete classes,** so everything else
   takes them by constructor injection (`docs/CODE_DESIGN_PRINCIPLES.md`
   → "Chosen tooling").
3. **Reach an autoload with `GetNode<T>("/root/<Name>")`.**
4. **Raise C# events for state changes,** not only Godot signals, so
   view-models can subscribe to them in tests.
5. **`RngProvider` is the app's one seeded RNG**
   (`docs/CODE_DESIGN_PRINCIPLES.md` §4).
6. **Pure logic goes in `libs/`, where it is tested;** a manager that
   needs heavy test setup does too much.
