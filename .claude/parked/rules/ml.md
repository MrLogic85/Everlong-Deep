---
paths:
  - "libs/EverlongDeep.ML/**"
  - "tests/EverlongDeep.ML.Tests/**"
---

# ML rules — `libs/EverlongDeep.ML`

The neural-network and genetic-algorithm engine.

## Rules

- **Plain values in and out:** primitives, small structs and Domain records
  such as `NeuralNetworkDef` and `NetworkPortLayout`, so the engine is
  tested with plain xUnit.
- **`double`, not `float`,** so training maths is reproducible.
- **No I/O, no global state:** no `System.IO`, `System.Net`, static mutable
  fields or `Console.WriteLine`.
- **No allocations in hot paths.** `NeuralNetwork.Forward` takes
  caller-owned buffers, which must be distinct arrays; the network keeps no
  per-call state.
- **Genes match by port, never by list order.** `DirectNetwork.Compile`
  matches saved genes to `NetworkPorts`, so a saved network loads whatever
  its order. A disabled gene compiles to 0 and evolution keeps it there.
  The `DirectNetwork` XML doc has the genome layout.
