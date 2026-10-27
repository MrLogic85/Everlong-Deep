---
paths:
  - "project/src/ui/**"
  - "project/scenes/screens/**"
  - "project/scenes/ui/**"
  - "project/scenes/widgets/**"
---

# UI rules — `project/src/ui/`

Screens and widgets.

## Rules

1. **Presentation only.** UI binds to view-models in `EverlongDeep.App`
   (`BuildViewModel`, not `Evolver`); no fitness maths, ML or persistence.
2. **Where it goes.** A Control that could be lifted into another Godot
   project unchanged belongs in `lib/`; one that uses project vocabulary
   ("creature", "generation", "network") is a widget in `widgets/`.
   Screens are in `screens/`. Their scenes are in `project/scenes/screens/`,
   `project/scenes/widgets/` and, for `lib/`, `project/scenes/ui/`. The
   scene owns the layout and the script the behaviour (`docs/UI_DIRECTION.md` → "Who owns what").
3. **A new screen, widget or gallery script joins `RewrittenUi`**
   (`tests/EverlongDeep.Ui.Tests/RewrittenSceneTests.cs`) in the same change,
   so the scene and source guards cover it.
4. **Numbers.** A screen script declares none: its exports take their value
   from its scene. A widget names every number, except literals inside a
   drawn widget's `_Draw` and its `Draw*` helpers. Never re-apply a size
   or padding the scene authors.
5. **Godot signals between Controls, C# events from view-models.** A
   screen unsubscribes in `_ExitTree()`.
6. **`RewrittenSceneTests` reads literal `GetNode<T>("%Name")` calls,** so a
   helper takes the node, not its path.

## Screens and Back

- Which scene is routed, and how it reaches saves and the navigator, is
  in `docs/ARCHITECTURE.md` → "Navigation".
- Android Back: add a `UiBackHandler` as its summary says; never set
  `QuitOnGoBack` in a screen. Anything else that acts on Back calls
  `UiBackPress.TryTake(this)` only once it has decided to act, since
  taking uses up the press.

## Text

`docs/LOCALIZATION.md` → "Text rules" owns what the text says; this is how
code shows it.

- Show a `UiText` with `label.ShowText(text)` or `button.ShowText(text)`;
  other components take it through their `…Source` properties and
  `UiTextTranslation.Source(text)`. Only `UiTextTranslation` reads a
  `UiText`.
- Text drawn in code asks `UiTextTranslation.Source` and redraws when the
  language changes.
- A label showing player-written text sets `auto_translate_mode = Disabled`
  on itself only. A part's own name crosses as `UiText.AsWritten`.
- Case shown text with `UiThemeLookup.LetterCase`, never .NET casing.
- A component that passes its text to an inner control calls
  `UiTranslation.ShareContext(this, inner)` first.
- A number argument is an `int`/`long` or a `FixedNumber`, wrapped with
  `UiText.Number`.

## Touch input

- A widget that hit-tests pointer input against what it draws maps the
  position with `GetGlobalTransformWithCanvas().AffineInverse() * position`;
  `ToLocal()` misses the world viewport's canvas transform.
- `UiWorldView` pushes events into its world; `BuildCanvas` reads them in
  `_UnhandledInput`. A tray drag maps through `SlotTransform`.
- Keep Godot's `emulate_mouse_from_touch` on. `BuildCanvas` is the one
  exception that reads `ScreenTouch`/`ScreenDrag` by index, skipping events
  whose device is `InputEvent.DeviceIdEmulation`.
- Keep `input_devices/pointing/android/enable_pan_and_scale_gestures` off:
  it doubles events and breaks pinch.

## Tests

Pure presentation helpers (number formatters, string builders) get unit
tests; `docs/MANUAL_TESTING.md` → "Decision point" decides when a UI change
needs a manual check.
