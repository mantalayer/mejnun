# Mejnun project rules

## Git commits

Do not add co-authorship attribution to commit messages.

## Localization (mandatory for every change)

Full guide: `Docs/Localization.md`. Design: `Docs/Mejnun-Plan.md` §7–§7.1.

1. **No raw player-facing strings.** Every word a player can read comes from a Unity String Table entry (`UI` or `DealerQuestions`):
   - UXML: bind with `<Bindings><UnityEngine.Localization.LocalizedString property="text" table="UI" entry="area.screen.item" /></Bindings>`; never write `text=`, `label=`, `tooltip=`, `title=` or `placeholder-text=` literals. If C# sets the text, the UXML element has neither the literal nor a binding.
   - C#: `Loc.Get("key")` / `Loc.Format("key", args)` from `Mejnun.Localization`, with the key as a string literal at the call site. Whole sentences with positional placeholders (`{0}`, `{0:plural:{} card|{} cards}`); never concatenate translated fragments.
   - Text set from C# re-renders on `Loc.LanguageChanged` (subscribe on init, unsubscribe on dispose).
   - Engine-free assemblies (`Mejnun.Core`, `Mejnun.AI`, `Mejnun.Net`) expose keys or enums; the UI resolves them.
   - Never send or save localized text; send keys/IDs.
   - Not localized: logs, exceptions, `*/Editor/*` tools, USS/element names, save/PlayerPrefs keys, Wwise names, user-typed text. Unavoidable exceptions are marked `// loc-ignore` (C#) or the `loc-ignore` class (UXML) and must be justified.
2. **Adding a string:** add the key with English text and a context comment to the `UI` table (Localization Tables window), or write a CSV `table,key,en,comment` and run
   `Unity -batchmode -quit -projectPath . -executeMethod Mejnun.Localization.Editor.LocalizationSourceImporter.ImportFromCommandLine -locSource <csv>`.
   Only write English; translations come from the Google Sheets pull. Keys are lowercase `area.screen.item` (see the area list in the guide); never rename or reuse a shipped key. Commit the changed `Assets/_Project/Localization/Tables/*` assets with the code.
3. **Validate before finishing:** run the `Mejnun.Localization.Tests` EditMode tests (or **Mejnun > Localization > Validate**). There must be no `RawString`, `UnknownKey` or `EmptySource` issues. Missing translations are warnings; they are filled by the sync.
4. **Translations before builds:** every player build runs `LocalizationPreBuild`. It validates, pushes new English to the Google Sheet, waits for `GOOGLETRANSLATE`, pulls the translations, and reports to `Logs/LocalizationReport.txt` (warnings only by default). To sync manually, run **Mejnun > Localization > Sync with Google Sheets**, or `-executeMethod Mejnun.Localization.Editor.LocalizationSheetsSync.SyncBatch`. Batch builds need `-locSync`.
5. **Platform language APIs:** the starting language comes from `PlatformLocaleSelector`, which uses the saved Settings choice, then `IPlatformServices.Language` (Steam `GetCurrentGameLanguage`/`GetSteamUILanguage`, iOS `NSLocale`, Android `LocaleList`, desktop `CultureInfo`), then English. New store backends must implement `ILanguageInfo` with the store's language first.
