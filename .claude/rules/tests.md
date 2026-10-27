---
paths:
  - "tests/**"
---

# Test rules — `tests/`

Test-code conventions. What each layer tests is in `docs/TEST_STRATEGY.md`
→ "Layers".

## Rules

- **Follow `docs/TEST_STRATEGY.md` → "Guiding principles"** and test
  nothing its "What we do NOT test" lists.
- **Mirror the production layout:** `libs/EverlongDeep.Simulation/Rumors/RumorSpread.cs`
  is tested in `tests/EverlongDeep.Simulation.Tests/Rumors/RumorSpreadTests.cs`.
  A test class is named for the class it tests, or for the family, owner
  or contract it covers.
- **Names describe behaviour:** `Spread_ToStranger_LowersConfidence`.
- **Arrange, then steps:** a scenario test arranges once, then runs its
  steps in paragraphs, each an act followed by its asserts.
- **Global usings live in each `*.Tests.csproj`,** not in each file.
- **Disk I/O only in a per-test temp directory.** Committed fixtures are
  read from the checkout and never written by a test.
