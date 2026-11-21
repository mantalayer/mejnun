# Release Candidate Checklist

## Localization and text rendering

- [ ] Run **Mejnun > Localization > Sync with Google Sheets**; confirm the latest English source is pushed and all translated cells are pulled.
- [ ] Run **Mejnun > Localization > Validate**. There must be no `RawString`, `UnknownKey`, `EmptySource`, `PlaceholderMismatch`, `ArabicPluralMismatch` or sheet-error issues; review missing-translation warnings before release.
- [ ] Confirm the Hindi `unlock.requirement.achievement` placeholder is `{0}` and the Persian wording reads naturally. Check all handwritten entries in `HandwrittenTranslations.csv` are present and marked reviewed in the workbook.
- [ ] Review all six Arabic plural forms (`zero|one|two|few|many|other`) with an Arabic speaker; verify pluralized timer, bot-fill and unlock text at 0, 1, 2, 3–10, 11–99 and 100+.
- [ ] Have native speakers review the Arabic and Persian front-end, HUD, settings, dialogs, dealer questions and narrow/mobile layouts. Record the reviewer, locale, build and disposition of each issue.
- [ ] Switch through all 21 locales in Settings. Check representative strings and nicknames with Latin, Arabic, Persian, Armenian, Greek, Japanese, Chinese, Korean, Vietnamese, Devanagari, Swahili and Thai characters.
- [ ] On `ar` and `fa`, confirm menus, HUD, dialogs, settings and navigation controls use the expected reading order while cards, board, hand and card art stay left-to-right. Check Arabic/Persian digits in timers and counts; verify nicknames/codes retain their typed digits.
- [ ] Verify `NotoSans` and the base fallback chain are assigned to both panel settings. For each CJK locale, confirm its addressable font group loads with no missing-font warnings and that the app remains playable when an optional font location is unavailable.
- [ ] Launch a player offline after the first download and confirm localized UI works without runtime translation requests or an API key.

## Accessibility and performance

- [ ] Verify four-colour cards show diamonds in blue and clubs in green, respect skins that opt out, and return to normal when disabled.
- [ ] Verify left-handed HUD control placement in LTR and RTL; confirm reduced motion, haptics, card size and all existing settings still work.
- [ ] On a mid-range mobile device, profile active play, another player's turn, two seconds of inactivity, app pause/resume and Battery Saver. Confirm the active rate is capped at the display refresh rate or 60 FPS, idle/battery-saving is capped at 30 FPS, and no animation becomes visibly choppy.
- [ ] Record device model, OS, measured frame rate, battery/thermal behavior and profiler capture. Keep on-device AI timing with the M3 follow-up.

## Crash reporting and build acceptance

- [ ] Confirm Cloud Diagnostics reporting is enabled and the Unity project is linked to the intended Unity Dashboard project.
- [ ] In a development build, run once with `-mejnunTestException`; confirm the test exception appears in the Cloud Diagnostics dashboard. Use `-mejnunTestCrash` only in a disposable development build/device because it deliberately crashes the process.
- [ ] Run the Unity Editor **Mejnun > Diagnostics > Send Test Exception** menu and confirm the expected exception is logged.
- [ ] Run focused EditMode localization tests and the release build's normal test suites; retain the test report and localization report with the candidate.
- [ ] Store listings are excluded from this checklist and remain deferred until M8 store setup is available.