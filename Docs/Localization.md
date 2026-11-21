# Localization

How player-facing text works in Mejnun, the rules every change must follow, and the translation pipeline (Unity String Tables ⇄ Google Sheets ⇄ `GOOGLETRANSLATE`). The design is in `Docs/Mejnun-Plan.md` §7 and §7.1.

## 1. The one rule

**No player-visible text is a raw string.** Every word a player can read comes from a String Table entry:

| Where the text is | How to write it |
|---|---|
| Static text in UXML (`text`, `label`, `tooltip`, `title`, `placeholder-text`) | A `LocalizedString` binding, never the attribute (see §3) |
| Text set from C# | `Loc.Get("key")` or `Loc.Format("key", args…)` (see §4) |
| Content lists (dealer questions) | Their own table; code picks keys with `Loc.KeysOf(Loc.DealerQuestionsTable)` |
| Data assets (deck, felt and achievement names) | A key derived from the stable ID (`cosmetic.deck.<id>.name`), resolved where it is shown |
| App name on the home screen / store | `UI/app.name` (written into iOS `Info.plist` and Android `strings.xml` at build time by the `AppInfo` metadata) |

Not localized: log and exception messages, editor tools (`*/Editor/*`), USS class and element names, PlayerPrefs and save keys, Wwise event names, network payloads, and anything the player typed (nicknames). Card ranks and suits are images. Language names in the language picker are endonyms (`Loc.NativeName`), so they are intentionally not translated.

**Never send localized text over the network or save it.** Send and store keys or IDs, and let each client resolve them in its own language.

A deliberate exception (a glyph such as `×`, the "MEJNUN!" logo word) is marked `// loc-ignore` at the end of the C# line or with the `loc-ignore` USS class on the UXML element. Use this sparingly; reviewers should challenge every one.

## 2. Tables and keys

- `UI`: all interface text. `DealerQuestions`: the opening questions (`dq.001`, `dq.002`, …).
- Keys are lowercase, dot-separated `area.screen.item`, with words joined by `_`: `menu.main.play`, `hud.button.end_turn`, `settings.audio.master_volume`, `achievement.ten_wins.name`.
- Areas in use: `app`, `common`, `menu`, `setup`, `online`, `picker`, `gallery`, `settings`, `rules`, `credits`, `hud`, `flavor`, `flow`, `results`, `table`, `deck_unlock`, `felt_unlock`, `tutorial`, `match`, `save`, `dealer`, `bot`, `net`, `achievement`, `cosmetic`, `unlock`, `profile`.
- `common.*` holds only generic words used in many places (`common.ok`, `common.cancel`, `common.back`, `common.close`, `common.on`, `common.off`, `common.yes`, `common.no`, `common.continue`, `common.done`, `common.locked`, `common.loading`). Anything with screen-specific meaning gets its own key, even if the English is the same: translations often differ by context.
- Never rename or reuse a key once it has shipped to the sheet. If the meaning changes, add a new key and delete the old one.
- Add a **comment** to every entry whose context is not obvious (what the placeholders are, where the text appears, length limits). Comments go into the entry's `Comment` metadata and help reviewers.

## 3. UXML

```xml
<ui:Button name="play-button" class="menu-button">
    <Bindings>
        <UnityEngine.Localization.LocalizedString property="text" table="UI" entry="menu.main.play" />
        <UnityEngine.Localization.LocalizedString property="tooltip" table="UI" entry="menu.main.play_tooltip" />
    </Bindings>
</ui:Button>
```

- Use `property="label"` for `Toggle`, `Slider` and other `BaseField` labels.
- If C# sets the element's text at run time, leave the UXML element without text **and without a binding**; the C# owns it (§4). A binding and a C# assignment on the same property fight each other.
- `DropdownField` choices are filled from C# with `Loc.Get` values.
- Bindings update automatically when the language changes.

## 4. C#

```csharp
using Mejnun.Localization;

title.text = Loc.Get("results.title.win");
cardsLeft.text = Loc.Format("hud.cards_left", count);           // "{0:plural:{} card left|{} cards left}"
banner.text = Loc.Format("hud.turn.player_turn", playerName);    // "{0}'s turn"
question.text = Loc.GetFrom(Loc.DealerQuestionsTable, questionKey);
```

- Pass the key as a **string literal at the call site**, so the validator can check it exists. For keys built from data (`"achievement." + id + ".name"`), put the builder in one helper and cover it with a test that every ID has its keys.
- Build whole sentences with placeholders. Never concatenate translated fragments (`Loc.Get("a") + name + Loc.Get("b")`); word order differs between languages.
- Placeholders are positional: `{0}`, `{1}`. Plurals use Smart String plural syntax: `{0:plural:{} card|{} cards}` (Arabic has six forms; the reviewer writes them by hand).
- Text set from code must refresh when the language changes: subscribe to `Loc.LanguageChanged` and re-run the presenter's refresh, and unsubscribe when the presenter is disposed.
- Engine-free assemblies (`Mejnun.Core`, `Mejnun.AI`, `Mejnun.Net`) cannot use `Loc`. They expose keys or enums, and the UI layer resolves them.

## 5. Adding a string

1. Add the entry to the `UI` table with its English text and a comment:
   - In Unity: **Window > Asset Management > Localization Tables**, select `UI`, and add the key with the `en` value (only English; leave other languages empty).
   - Scripted, for agents and bulk changes: write a CSV with the header `table,key,en,comment` and run **Mejnun > Localization > Import English Source CSV…**, or in batch mode:
     ```
     Unity -batchmode -quit -projectPath . \
       -executeMethod Mejnun.Localization.Editor.LocalizationSourceImporter.ImportFromCommandLine \
       -locSource path/to/strings.csv
     ```
2. Reference it from UXML (§3) or C# (§4).
3. Run **Mejnun > Localization > Validate** (or the EditMode tests). There must be no `RawString`, `UnknownKey` or `EmptySource` issues.
4. Commit the changed table assets (`Assets/_Project/Localization/Tables/*`) with the code.

### Importing popup lines and dealer questions

Use **Mejnun > Localization > Import Popups & Questions Sheet** to import the English source from the `Good Popups`, `Bad Popups` and `Deciding Questions` tabs. Put English in column A; the importer reads or fills stable keys in column B (it adds a `key` header when column A has a recognized header and B1 is blank). It writes good/bad lines to `UI` keys (`flavor.good.NNN` / `flavor.bad.NNN`) and questions to `DealerQuestions` keys (`dq.NNN`), then saves a `table,key,en,comment` snapshot at `Assets/_Project/Localization/Source/PopupSheet.csv`.

On the first import, lines without keys are matched to existing active entries by trimmed English text (case-insensitive); unmatched lines get the next unused number, including numbers occupied by retired entries. The assigned key is written back to column B. On later imports, a valid key in column B is authoritative, so changing a line to fix a typo updates that same localization entry. Duplicate English rows are imported only once and the duplicate row's key cell is cleared. Entries removed from a tab are marked retired in shared table metadata: they are preserved and never reused, but are omitted from runtime content lists such as popup selection and dealer questions. Re-adding a retired line without its former key creates a new key; restoring its original key in column B reactivates it.

The importer uses the configured `SheetsServiceProvider` OAuth client and the separate `PopupSpreadsheetId` setting in `LocalizationPipelineSettings`, so it does not use or change the translation-sync workbook ID (`SpreadsheetId`). It resolves each tab by its configured gid first, then falls back to the expected tab name (`Good Popups`, `Bad Popups` or `Deciding Questions`) if a gid has changed. It reads columns A and B and reports added, updated and retired entries. Batch users can override the popup workbook ID and tab IDs when needed:

```
Unity -batchmode -quit -projectPath . \
  -executeMethod Mejnun.Localization.Editor.LocalizationSheetImporter.ImportFromCommandLine \
  -popupSheetId <spreadsheet-id> [-goodPopupsGid <gid>] [-badPopupsGid <gid>] [-decidingQuestionsGid <gid>]
```

The `UI` entry `hud.invalid_move.flavor` formats the optional bad-move line and the stock rule explanation as `{0}\n{1}`. Good-move flavor lines are shown on a one-in-three chance; if a line is unavailable or not selected, the existing localized popup is used.

Translations normally arrive through the sheet (§6) the next time someone syncs or builds. Reviewed exceptions are kept in `HandwrittenTranslations.csv` and reapplied after each pull; do not edit translated table assets by hand.

Deck skin and table felt names are copied from each asset's serialized English `displayName` into the `UI` entry derived from its stable ID when the asset is saved. To sync all names immediately, use **Mejnun > Localization > Sync Deck & Felt Names**; the next Sheets sync then updates changed English cells, refreshes frozen auto-translations, and marks reviewed translations as needing review.

## 6. Translation pipeline

```
 Unity String Tables ──push Key + en──▶ Google Sheet ──GOOGLETRANSLATE──▶ locale columns
        ▲                                                                     │
        └──────────────── pull locale columns (never Key / en / status) ◀─────┘
```

- **Before every player build** `LocalizationPreBuild` validates all tables. If any translation is missing or broken, it runs the sync: push new and changed English, add `GOOGLETRANSLATE` formulas to new rows, wait for Google to evaluate them, then pull. It then validates again and writes `Logs/LocalizationReport.txt`. Problems are **warnings only** (English is shown for any gap); set `FailBuildOnIssues` in `LocalizationPipelineSettings` to make them block builds.
- Run the same round trip at any time with **Mejnun > Localization > Sync with Google Sheets**, or in batch mode with `-executeMethod Mejnun.Localization.Editor.LocalizationSheetsSync.SyncBatch`.
- Batch-mode builds only sync when `-locSync` is passed, because the first Google sign-in opens a browser.
- **Sheet layout** (one tab per collection: `UI`, `DealerQuestions`): `Key | en | ar | ar status | fa | fa status | …`. New rows get `=IF($B2="","",GOOGLETRANSLATE($B2,"en","<code>"))` and the status `auto`.
- **Review:** a reviewer replaces a formula with plain text and sets that locale's status to `reviewed`. Pushes preserve reviewer-entered translations; when English changes, reviewed rows keep their text and become `needs-review`. Explicit `HandwrittenTranslations.csv` overrides are pushed as plain text with `reviewed` status.
- **Avoiding repeated translation:** after pulling, sync replaces each completed `GOOGLETRANSLATE` formula with its result as plain text (status remains `auto`), so adding a new line does not recalculate every existing translation. The first sync after this change freezes ready existing formulas; any formula still showing `Loading...` is left in place for a later sync. If English changes on an `auto` or blank-status row, sync restores its formula to refresh the translation. Set the status to `reviewed` when editing a translation by hand, or the next English change will re-translate it.
- **Stuck `Loading...` cells:** Sync clears and re-enters stuck translation formulas once before pushing. After waiting for `GOOGLETRANSLATE`, any still-empty, loading or erroring formula is translated through Google's direct translation endpoint and written back as plain text; the report shows the number recovered. This fallback uses an unofficial endpoint, so if it fails the cell is left unchanged and the warning is logged.
- **Guardrails** (checked by the validator after every pull): placeholders (`{0}`, `{0:plural:…}`) and brace balance match English; Arabic plural blocks have six alternatives; no `#VALUE!`, `#N/A` or `Loading...` cell values. Missing translations are warnings until the sheet formulas finish and are pulled.
- **Adding a language:** add the locale and its `GOOGLETRANSLATE` code to `LocalizationProjectSetup.TargetLocales`, then run **Mejnun > Localization > Set Up Project**. Setup creates the Locale and tables, and merges any missing locale columns into `LocalizationPipelineSettings`; the next sync adds its sheet columns and formulas. No other game code or manual settings-asset edit is needed.

### One-time Google setup

1. In Google Cloud, create a project, enable the **Google Sheets API**, and create an **OAuth client ID** of type *Desktop app*.
2. In Unity: **Assets > Create > Localization > Google Sheets Service**, and enter the client ID and secret (keep this asset out of public repos; it is ignored by the build).
3. Create the workbook, share it with the Google account you will sign in with, and copy its ID from the URL.
4. Select `Assets/_Project/Localization/LocalizationPipelineSettings.asset`, assign the Sheets Service asset and set `SpreadsheetId` for translation sync. Set `PopupSpreadsheetId` separately for the popup/questions source workbook.
5. Run **Mejnun > Localization > Sync with Google Sheets** once. A browser window opens for sign-in, and the token is cached for later syncs and builds.

### Fonts, right-to-left layout and digits

- Run **Mejnun > Localization > Set Up Fonts** after adding or replacing font files. It creates dynamic multi-atlas TextCore font assets and the shared `PanelTextSettings` used by both UI panel settings.
- The base chain is Noto Sans, Vazirmatn, Noto Sans Armenian, Noto Sans Devanagari and Noto Sans Thai. Each font's `OFL.txt` is kept beside its source file. Japanese, Korean, Simplified Chinese and Traditional Chinese fonts are separate Addressables groups (`Fonts-ja`, `Fonts-ko`, `Fonts-zh-Hans`, `Fonts-zh-Hant`) and are loaded with the active locale first by `FontChainLoader`.
- Shared UI roots select the advanced Text Generator and the Noto Sans default. `UiDirection` follows `Loc.LanguageChanged` and applies `.rtl`/`LanguageDirection.RTL` for Arabic and Persian; corresponding USS rules reverse menus, HUD controls, dialogs and settings rows. Current back/continue actions are localized text buttons (there are no separate directional icon assets to mirror). The table, cards and player hand stay left-to-right (decision Q-D).
- `LanguageDirection.RTL` orders the characters of a line but never changes alignment: `-unity-text-align` defaults to upper-left in every language. Each left-aligned text class therefore needs a matching `.rtl` rule (`upper-right` for paragraphs, `middle-right` for single-line labels) in `table.uss`, `frontend.uss` or `flow.uss`. Centred text needs nothing, but add an explicit `.rtl` centring rule when a centred element shares a class with paragraph text (`rule-body`, `tutorial-prompt-copy`). Scroll views with paragraphs also reverse `unity-scroll-view__content-and-vertical-scroll-container` so the scrollbar sits on the left. When you add a text class, add it to the `.rtl` list; `FrontEndFlowTests.RulesParagraphsAlignRightInRightToLeftLocales` covers the rules page.
- `Loc.Format` and `Loc.Number` convert ASCII digits to Eastern Arabic digits for `ar` and Persian digits for `fa`. String arguments are protected while formatting so nicknames and codes retain their original digits.
- Handwritten exceptions belong in `Assets/_Project/Localization/HandwrittenTranslations.csv`, not directly in translated String Table cells. They are applied after pulls and pushed as plain text with `reviewed` status. Use six Arabic plural alternatives in `zero|one|two|few|many|other` order; `LocalizationValidator` reports Arabic plural blocks that do not contain six alternatives.
- Verify fonts and RTL rendering in a running player for all scripts and specifically review Arabic and Persian layouts, including narrow/mobile screens. The source fonts and runtime fallback behavior cannot be validated by the String Table validator alone.

## 7. Language selection at run time

Startup order (`LocalizationSettings` startup selectors):

1. `-language=xx` on the command line (testing).
2. `PlatformLocaleSelector`:
   1. The player's choice from **Settings > General > Language** (`settings.language`; empty means *Automatic*).
   2. The platform's languages through `IPlatformServices.Language`:
      - **Steam:** `SteamApps.GetCurrentGameLanguage()` (the language picked for this game in the Steam client), then `SteamUtils.GetSteamUILanguage()`, then the OS list. Steam has no Persian, Armenian, Hindi or Swahili, so when the OS prefers `fa`, `hy`, `hi` or `sw`, the OS wins.
      - **iOS:** `NSBundle.preferredLocalizations` + `NSLocale.preferredLanguages` (`Assets/_Project/Plugins/iOS/MejnunLocale.mm`), which includes the iOS per-app language setting.
      - **Android:** `LocaleList.getDefault()` (includes the Android 13+ per-app language).
      - **Desktop / Editor:** `CultureInfo.CurrentUICulture`, then `Application.systemLanguage`.
   3. Tags are matched to shipped locales by `LocaleMatcher`. For example, `pt-PT` becomes `pt-PT`, `pt-AO` becomes `pt-PT`, bare `pt` becomes `pt-BR`, `zh-CN` becomes `zh-Hans`, `zh-TW` becomes `zh-Hant`, and `es-419` becomes `es`.
3. English.

Store-side localization is managed outside the game: the Steam store page, Steam achievement names and descriptions (Steamworks *Stats & Achievements > Localization*), App Store and Play listings and Game Center / Play Games achievement names. Use the `achievement.*` entries of the `UI` table as the source text for those consoles. Localized store listings remain a separate deferred M9 item; there is no `StoreListings` sheet tab yet.

## 8. Tools reference

| Menu item | Code |
|---|---|
| Mejnun > Localization > Set Up Project | `LocalizationProjectSetup.Run` (idempotent: locales, tables, selectors, `AppInfo`) |
| Mejnun > Localization > Validate | `LocalizationValidator.Validate` → `Logs/LocalizationReport.txt` |
| Mejnun > Localization > Import English Source CSV… | `LocalizationSourceImporter.ImportCsv` |
| Mejnun > Localization > Import Popups & Questions Sheet | `LocalizationSheetImporter.ImportSheet` |
| Mejnun > Localization > Sync with Google Sheets | `LocalizationSheetsSync.Sync` |
| Mejnun > Localization > Apply Handwritten Translations | `HandwrittenTranslationApplier.ApplyAll` |
| Mejnun > Localization > Set Up Fonts | `FontChainSetup.SetUpFonts` |
| (automatic, every player build) | `LocalizationPreBuild` |
