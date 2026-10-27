---
paths:
  - "project/src/ui/**"
  - "project/scenes/screens/**"
  - "project/scenes/ui/**"
  - "project/scenes/widgets/**"
  - "project/assets/themes/**"
  - "project/assets/icons/**"
  - "project/project.godot"
---

# UI library rules — `project/src/ui/lib/`

The component library: reusable, app-agnostic Controls, their scenes in
`project/scenes/ui/`, the theme files and the dimension constants. This
file says how its code is written, and its theme, drawing, input and level
rules bind all UI code; `.claude/rules/ui.md` has the screen and widget
rules. `docs/UI_DIRECTION.md` owns how it looks.

## Boundaries

- No references to `project/src/sim/`, `project/src/creature/` or
  `EverlongDeep.Domain`; only primitives and system types.
- Parameters are `[Export]`s with defaults that render in the editor,
  events are signals, one file per Control.
- A plain `Control` returns a computed size from `_GetMinimumSize`, leaving
  `custom_minimum_size` to the scene. Button, Label and container
  subclasses cannot (their native minimum size ignores the script), so
  those own their `custom_minimum_size`.
- A `[Tool]` component that writes a property on itself or a node of its
  own scene lists it in a `UiUnsavedState` and calls `Handle` first in
  `_Notification`, or Godot saves the derived value in the scene. Add
  own-node properties to `SceneDerivedStateTests`.
- Never copy a component's generated internals into a scene; fill its slot
  through Editable Children.

## Theme and tokens

- **The Theme owns what differs between palettes:** colours, opacities
  (`UiTokens.Alpha`), `effects_enabled` and type variations. **Constants
  own dimensions** (`UiSize`, `UiLayout`, `UiSpacing`): every palette
  shares them, pure tests can use them, and Godot would round them as
  theme constants. Never re-declare one locally; an editor-picked
  dimension is a `UiTokens` identifier that `UiThemeLookup.Size` maps.
- **Select theme values by name, never by copying them.** Set
  `ThemeTypeVariation`; a value read and written back as an
  `AddTheme*Override` goes stale on a theme swap. A control has one
  variation, so combined axes (typography × text colour) are variations
  `UiThemeExpander` generates and chains. Read drawn colours in `_Draw`,
  which Godot re-queues on a theme change. Overrides are only for
  per-instance values (`IconTint`); never pass palette data to controls or
  walk a subtree to propagate style.
- A soft fill is a base colour
  `.WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft))`; colour
  maths is `UiColorExtensions`.
- Text takes its style with `UiThemeLookup.ApplyTextStyle(control,
  typography, color)` (or `UiLabel`'s Text Style and Text Color), never
  `ApplyTypography` plus a copied `font_color`.
- **Theme files in `project/assets/themes/` are the source of truth.**
  `Neon.tres` is the project theme: the Neon palette plus everything
  palettes share. Its default separation, 8, equals `UiSize.Space.S2`;
  change both together. `Paper.tres` holds only its palette; a colour
  missing there silently falls back to Neon.
- Colour-derived items (`UiThemeExpander.DerivedColors`) are generated:
  never hand-edit them; regenerate them as `.claude/rules/tools.md` says.
- Each typography's `FontVariation` sets `spacing_top`/`spacing_bottom` to
  reach the token's `lineHeight`: top = floor(adjustment / 2), bottom the
  rest, adjustment = `lineHeight` − font height. Recompute when a style's
  size or line height changes.
- Screens inherit the app's palette, which `UiAppTheme` sets on the root
  window. Assign another `Theme` only to switch palette, in `_EnterTree`
  or before `AddChild`, never in `_Ready`: authored children run `_Ready`
  first and would measure against Godot's default theme.
- Any node that is neither a Control nor a Window cuts Godot's theme
  inheritance. Where one must sit between Controls, such as a
  `CanvasLayer` or a world's SubViewport, the Controls under it take
  `UiThemes.NearestOf` the node above it; they never pick a palette.
- A `NotificationThemeChanged` handler is only for caches that cannot be a
  variation (draw geometry, styleboxes, icon tints). Godot sends it on every
  tree entry, before `_Ready`, so a rebuilding handler checks
  `IsNodeReady()`. One that writes overrides on its own node re-triggers
  itself; wrap it in `UiThemeRefresh.Guarded`. An override of
  `_Notification` calls `base._Notification(what)`.

## Drawing

- **Every stroke in `_Draw` goes through `UiPixelPen`,** thin filled rects
  (dividers) included: Godot's antialiased primitives feather about one
  unit, which the stretch and UI size blow up into a blur. Godot cannot
  feather `DrawColoredPolygon`, so keep a polygon's edge under an
  antialiased outline, or draw a square or diamond as one wide `pen.Line`.
- Many strokes of one colour go in one `pen.Strokes` call: on
  Compatibility each draw call builds its own buffer, which costs a phone
  its frame.
- A part visual bakes the pixel scale into its strokes, so every view that
  holds parts calls `PartVisual.RedrawOnNewPixelScale` whenever it may have
  zoomed, including while paused.
- Keep `StyleBoxFlat.AntiAliasing` on: `DrawStyleBox` feathers correctly.

## Icon filtering

Icons sample `Linear`; text and other textures keep the project's
`Nearest`. At a fractional UI size `Nearest` doubles or drops icon rows,
and text under `Linear` looks soft. `UiIconFilterGuardTests` says which
call each icon host makes; a `Button` that draws its own text beside an
icon wraps the icon in `UiLinearIcon`, because Godot's `CanvasTexture`
draws a `DPITexture` unscaled.

SVGs stay white and take colour from modulate or icon colours, never
`color_map`. Button icons use `UiIcons.Apply` without a tint; state colours
come from `Button` or a generated variation.

## UI size and worlds

- The `UiScale` autoload applies the UI size once, as the root window's
  `ContentScaleFactor` (`UiScale.RootFactor`). Nothing else multiplies by
  it: tokens, scenes and components keep their numbers.
- That factor scales every canvas item, so a world view undoes it:
  `UiWorldView` sizes its SubViewport at the slot times `RootFactor`, and
  its screen-size overlays apply the factor again. Build's world sets
  `ScalesWithUi` instead.
- A thumbnail world's SubViewport update mode is Disabled; it calls
  `RequestRender` when it refreshes, so scrolling renders nothing. Each
  world's render target costs about 1 MB on a phone, so a long list reuses
  its cards. Every world sets `disable_3d`.

## Input and press feedback

- **No hover look,** since hover only lingers after a tap; the held look is
  `UiPressFeedback`.
- **One input source per tap.** Touch reaches controls as Godot's emulated
  mouse, so a control reads mouse events (`PointerInput`), never touch too,
  which would handle a tap twice.

## UI levels

- Order drawing by the tree, never `ZIndex`: add an overlay last, or a part
  that must draw last with `InternalMode.Back`. Marks in a world go on a
  `ViewLayer`, since the parts sit on `CreatureLayers`
  (`.claude/rules/theme.md`).
- The levels of a Viewport are the `UiLayers` CanvasLayers: `Screen`,
  `Overlay` (menus), `Notification`. A dialog is an embedded Window over
  every level of its Viewport, so it covers notifications; dialogs stay in
  the scene that opens them.
- A menu that floats over its opener sits in a `UiLevelLayer`, which stays
  in the opener's subtree (lifetime, unique names) and hands on the theme
  and visibility a `CanvasLayer` cuts.

## Clipping and glow

- Glow draws outside the layout rectangle. Never add layout padding to show
  it; a clipping parent gives bleed or accepts clipped glow, and popups
  live in an unclipped layer.
- Godot cannot nest `clip_children` (the inner node draws nothing), so
  `UiCard.ClipContent` and `UiMenu` clip through `UiClip` only when no
  ancestor does. Content flush with an unclipped card's edge rounds the
  corners itself (`UiCorners`). `ClipContent` is off by default: each
  clipping card renders through an extra buffer.

## Components

- `UiButton` is the only button class. Author `IconId` (`UiIconId.None`,
  never null) and `Kind = Flat`, not the native `Icon` or `Flat`;
  `ContentLayout` picks the icon size, never the call site.
- A button group with a look of its own implements `IUiButtonDesigner`
  rather than restyling its `UiButton`s.
- A stepped slider sets `UiSlider.Step` from the value's `SettingRange`.
  Never round the value alone: the thumb and readout disagree and the thumb
  jumps. Uneven stops use `SettingRange.Of(stops)`.
- A message in a drawing goes through `UiCalloutLayer`, fed by the
  view-model's notes, most important first.
- A bar's `UiTextField` has a fixed width: `LineEdit` has no "…", and no
  scene setting caps a text-sized width at the space left.
- Layout widths a scene authors have no C# constant, since a scene cannot
  read one. The reference values: `w-dialog` 300, `w-card` 326, `w-sheet`
  720, `w-sheet-wide` 880, `w-brain` (the network sheet) 460, `w-well` 250,
  `h-well` 64, `w-tile` 156, `h-stage` 170, `h-thumb` 100, `w-col-xs` 40,
  `w-col-md` 76, `w-col-lg` 96, `w-col-xl` 128, `w-form` (Settings'
  column) 400.
- `UiDialogContent.tscn` and `UiNotificationContent.tscn` are the runtime
  layout, not mocks: keep their `%` names.
- The gallery pages share `GalleryScreen`; each page scene authors its own
  `UiFrame`, toolbar and menu under the same unique names.
  `ui/component_gallery=true` or `ui/popup_gallery=true` makes a
  development export start on that page.
