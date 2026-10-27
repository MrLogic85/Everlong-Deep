---
paths:
  - "project/src/theme/**"
---

# Theme rules — `project/src/theme/`

How the arena and creatures look: theme colours, drawing helpers and the
part visuals Build, Training and thumbnails share. `docs/WORLD_VISUALS.md`
owns how parts look and their draw order.

## Rules

1. **Plain visual state only.** Code here takes geometry, a `VisualTheme`,
   selection and display state. It never depends on physics bodies,
   runtime sensors, view-models or screens; the creature and UI layers
   adapt their own state into it.
2. **Draw layers are named.** A part passes its `DrawSlot` (or a
   `CreatureLayers` member, outside the draw groups) to `PartVisual`'s
   constructor, and only `PartVisual` sets its `ZIndex`, so the
   selected-surface switch is never bypassed. Whatever holds the parts
   calls `PartVisual.Place` on each, with `DrawGroups.Raised`, whenever the
   shape or selection changes; an unplaced part draws over every group. A
   part that draws in two slots draws the second on a child from
   `PartVisual.AddSlotLayer`.
3. **Selection is drawn with its part.** A part draws its own marks in its
   `_Draw`, or on a child layer of its own that turns with it, and rises
   whole to the selected surface while selected or raised; never on a
   separate layer. `JointPart` draws the marks of the Touch sensor's
   cushion and the Rubber's skin. Two exceptions:
   - A Wheel's mark is drawn by its `JointPart` after its bands, so it lies
     over the links and the cushion.
   - A selected joint motor's, Brake's or Angle sensor's Fixed and Target
     link bands lie along other parts' links, so the Build canvas draws
     them on `CreatureLayers.SelectedUnderlays`, under the selected
     surface.
4. **Theme values are read through the `VisualTheme` handle, never
   copied.** Take it from `VisualTheme.For(node)` for the node's world, or
   from whatever holds the parts, and read a value where it is used,
   normally in `_Draw`; the handle redraws its world on a theme change.
   A value kept outside `_Draw`, such as a `Polygon2D`'s colour or a
   `Modulate`, is set through `VisualTheme.Bind`.
