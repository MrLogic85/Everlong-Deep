---
name: add-language
description: Add a translation of Everlong Deep into a new language, from the .po file to checking the game in it. Use when asked to add, start or register a language or translation.
---

# Add a language

`docs/LOCALIZATION.md` owns the rules.

1. Check `project/locale/messages.pot` is current; if the test fails,
   regenerate it as "The template" says:

   ```bash
   dotnet test tests/EverlongDeep.Ui.Tests --filter TranslationTemplateTests
   ```

2. Create `project/locale/<language>.po` from the template in Poedit (New
   from POT) or with gettext. `--no-translator` keeps your name and email
   out of the committed file; check the headers either way, and set
   `Language` and `Plural-Forms` for the language:

   ```bash
   msginit --no-translator --locale=<language> -i project/locale/messages.pot -o project/locale/<language>.po
   ```

3. Translate every entry by "Adding a language". Check the header and
   plural forms with `msgfmt --check -o /dev/null project/locale/<language>.po`;
   it does not check the `{0}` placeholders, so compare those by hand.
4. Register the file in Project Settings → Localization → Translations
   (`internationalization/locale/translations` in `project/project.godot`),
   and turn on the text server data ("Text server data").
5. Check the game in the language with the `desktop-check` and
   `android-check` skills, with the system set to it. A native speaker
   judges the wording; leave that to the human.
