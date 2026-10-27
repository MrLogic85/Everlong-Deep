---
paths:
  - "libs/EverlongDeep.App/**"
  - "tests/EverlongDeep.App.Tests/**"
---

# App rules — `libs/EverlongDeep.App`

View-models, services and repositories in plain C#: the bridge between the
Godot side and the pure layers.

## Rules

- **Every service Godot code consumes has an interface,** and callers use
  it, so tests can substitute it.
- **Save JSON goes through `SaveJson`,** the one strict setup for every
  save file.
- **A `SaveMigration` edits the JSON one version up** and throws
  `InvalidDataException` for a file it cannot change, which then loads as a
  bad file. Any other exception is a bug and is not caught.
- **Never call `ICreationUpdateCoordinator.Get` while holding a creation's
  lock** (inside an `UpdateIfPresent` update) **or from a queued save:**
  `Get` waits for that creation's queued saves, so it deadlocks.
- **File paths come from `IStorageLocation`,** so Godot can inject
  `user://` paths on Android.
- **In-memory fakes are production code beside their interface**
  (`InMemoryCreationRepository`). Tests fake repositories with them and
  other services with NSubstitute.
