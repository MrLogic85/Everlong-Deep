---
name: change-app-icon
description: Change Everlong Deep's launcher icon or Android launch splash and check it on a device. Use when asked to change, redraw or fix the app icon, its themed (monochrome) layer or the launch splash.
---

# Change the app icon

`docs/UI_DIRECTION.md` → "App icon" owns the design, and
`docs/RELEASING.md` → "App icon" the files.

1. Agree the change with the human and the `design-lead` agent, and
   update the design in `docs/UI_DIRECTION.md` → "App icon" first if it
   changes.
2. Edit the SVGs in `project/assets/icons/app/` as `docs/RELEASING.md` →
   "App icon" says, starting with the one that owns the art.
3. Run the icon tests:

   ```bash
   dotnet test tests/EverlongDeep.Ui.Tests --filter AppIconTests
   ```

4. Check it on a device with the `android-check` skill: the launcher icon,
   a themed icon on Android 13+, and the launch splash. Attach the
   screenshots to the PR.
