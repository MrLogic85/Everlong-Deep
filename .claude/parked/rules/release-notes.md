---
paths:
  - "docs/release-notes/**"
---

# Release-note rules — `docs/release-notes/`

One file per published version, `X.Y.Z.md`, used unchanged as the GitHub
release text (`docs/RELEASING.md` → "Releasing a milestone"). The
`write-release-notes` skill has the steps; the owner approves the text in
the notes PR.

- **Sources:** the milestone's closed issues and the PRs merged since the
  previous release from `main`, minus fixes a fix release has already
  shipped (for a fix release, the fixes on `release/vX.Y`). Check what
  actually changed for the player, not only the titles.
- **Audience:** players, in English. Say what they can now do, not how it
  was built.
- **Focus:** only the few biggest new things, told so a player wants to try
  them; not every change and not a guide. Renames, polish and small
  improvements stay out.
- **Fixes:** name one only when players clearly felt the bug, such as a
  crash or lost creations, in one plain sentence under `## Fixed`.
- **Leave out:** issue and PR numbers, code, class or file names,
  refactors, tests, CI and tooling.
- **Shape:** a one-line summary, then one `### ` section per new thing, each
  one to three short, plain sentences. No bullet lists and no top-level
  title (GitHub shows "Everlong Deep X.Y.Z"). A release that players install
  over an older one keeps an `## Install` section and may open with a short
  callout on what carries over.
- **Images:** a landscape screenshot for most sections, showing the new
  thing in use, with one example creation across the images. Crop the black
  edge on the left; links are absolute GitHub user attachments, so they
  work on the release page. Alt text says what the picture shows.
- **Check:** every sentence against the code and the previous notes, so
  nothing old is sold as new.
