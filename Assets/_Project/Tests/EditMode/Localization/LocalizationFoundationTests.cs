using System.Linq;
using System.Collections.Generic;
using Mejnun.Game;
using Mejnun.Localization.Editor;
using Mejnun.Platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Mejnun.Localization.Tests
{
    public sealed class LocalizationFoundationTests
    {
        private static readonly string[] Shipped =
        {
            "en", "ar", "fa", "hy", "es", "de", "el", "it", "fr", "ja", "zh-Hans",
            "pt-BR", "ko", "zh-Hant", "vi", "nl", "pt-PT", "ru", "hi", "sw", "th"
        };

        [TestCase("en-US", "en")]
        [TestCase("de_AT", "de")]
        [TestCase("pt-PT", "pt-PT")]
        [TestCase("pt", "pt-BR")]
        [TestCase("pt-AO", "pt-PT")]
        [TestCase("es-419", "es")]
        [TestCase("zh-CN", "zh-Hans")]
        [TestCase("zh-Hans-SG", "zh-Hans")]
        [TestCase("zh", "zh-Hans")]
        [TestCase("zh-TW", "zh-Hant")]
        [TestCase("ko-KR", "ko")]
        [TestCase("ru-RU", "ru")]
        [TestCase("hi-IN", "hi")]
        [TestCase("sw-KE", "sw")]
        [TestCase("th-TH", "th")]
        [TestCase("vi-VN", "vi")]
        [TestCase("nl-BE", "nl")]
        [TestCase("fa-IR", "fa")]
        [TestCase("hy-AM", "hy")]
        [TestCase("ar-EG", "ar")]
        [TestCase("el-GR", "el")]
        public void LocaleMatcherMapsPlatformTagsToShippedLocales(string tag, string expected)
        {
            Assert.That(LocaleMatcher.MatchOne(tag, Shipped), Is.EqualTo(expected));
        }

        [TestCase(SystemLanguage.Vietnamese, "vi")]
        [TestCase(SystemLanguage.Hindi, "hi")]
        [TestCase(SystemLanguage.Thai, "th")]
        [TestCase(SystemLanguage.Greek, "el")]
        public void SystemLanguageMapsNewLanguagesToLocaleCodes(SystemLanguage language, string expected)
        {
            Assert.That(DeviceLanguageInfo.FromSystemLanguage(language), Is.EqualTo(expected));
        }

        [TestCase("tr")]
        [TestCase("pl")]
        [TestCase("")]
        public void LocaleMatcherRejectsUnshippedLanguages(string tag)
        {
            Assert.That(LocaleMatcher.MatchOne(tag, Shipped), Is.Null);
        }

        [Test]
        public void LocaleMatcherUsesTheFirstSupportedPreference()
        {
            Assert.That(LocaleMatcher.Match(new[] { "ko-KR", "zh-TW", "fr-CA", "en" }, Shipped), Is.EqualTo("ko"));
        }

        [TestCase("english", "en")]
        [TestCase("schinese", "zh-Hans")]
        [TestCase("tchinese", "zh-Hant")]
        [TestCase("brazilian", "pt-BR")]
        [TestCase("portuguese", "pt-PT")]
        [TestCase("latam", "es-419")]
        [TestCase("arabic", "ar")]
        [TestCase("greek", "el")]
        [TestCase("koreana", "ko")]
        public void SteamApiLanguagesMapToLanguageTags(string steam, string expected)
        {
            Assert.That(SteamLanguageCodes.ToLanguageTag(steam), Is.EqualTo(expected));
        }

        [Test]
        public void SteamLanguagesResolveToShippedLocales()
        {
            Assert.That(LocaleMatcher.MatchOne(SteamLanguageCodes.ToLanguageTag("brazilian"), Shipped), Is.EqualTo("pt-BR"));
            Assert.That(LocaleMatcher.MatchOne(SteamLanguageCodes.ToLanguageTag("portuguese"), Shipped), Is.EqualTo("pt-PT"));
            Assert.That(LocaleMatcher.MatchOne(SteamLanguageCodes.ToLanguageTag("tchinese"), Shipped), Is.EqualTo("zh-Hant"));
            Assert.That(LocaleMatcher.MatchOne(SteamLanguageCodes.ToLanguageTag("greek"), Shipped), Is.EqualTo("el"));
            Assert.That(SteamLanguageCodes.IsSupportedBySteam("fa-IR"), Is.False);
            Assert.That(SteamLanguageCodes.IsSupportedBySteam("hy"), Is.False);
            Assert.That(SteamLanguageCodes.IsSupportedBySteam("hi-IN"), Is.False);
            Assert.That(SteamLanguageCodes.IsSupportedBySteam("sw-KE"), Is.False);
            Assert.That(SteamLanguageCodes.IsSupportedBySteam("de-DE"), Is.True);
        }

        [Test]
        public void ProjectHasEveryShippedLocaleWithEnglishFallback()
        {
            var codes = LocalizationEditorSettings.GetLocales().Select(l => l.Identifier.Code).ToList();
            Assert.That(codes, Is.EquivalentTo(Shipped));
            Assert.That(LocalizationSettings.ProjectLocale.Identifier.Code, Is.EqualTo("en"));
            foreach (var locale in LocalizationEditorSettings.GetLocales().Where(l => l.Identifier.Code != "en"))
            {
                var fallback = locale.Metadata.GetMetadata<UnityEngine.Localization.Metadata.FallbackLocale>();
                Assert.That(fallback?.Locale?.Identifier.Code, Is.EqualTo("en"), locale.Identifier.Code);
            }

            var pipelineSettings = LocalizationPipelineSettings.Load();
            Assert.That(pipelineSettings, Is.Not.Null);
            foreach (var (code, google) in LocalizationProjectSetup.TargetLocales)
            {
                var column = pipelineSettings.TargetLocales.SingleOrDefault(target => target.LocaleCode == code);
                Assert.That(column?.GoogleLanguageCode, Is.EqualTo(google), code);
            }
        }

        [Test]
        public void NativeLanguageNamesDistinguishScriptAndRegionVariants()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var simplifiedChinese = Loc.NativeName(locales.Single(locale => locale.Identifier.Code == "zh-Hans"));
            var traditionalChinese = Loc.NativeName(locales.Single(locale => locale.Identifier.Code == "zh-Hant"));
            var brazilianPortuguese = Loc.NativeName(locales.Single(locale => locale.Identifier.Code == "pt-BR"));
            var europeanPortuguese = Loc.NativeName(locales.Single(locale => locale.Identifier.Code == "pt-PT"));

            Assert.That(simplifiedChinese, Is.Not.Empty);
            Assert.That(traditionalChinese, Is.Not.Empty);
            Assert.That(brazilianPortuguese, Is.Not.Empty);
            Assert.That(europeanPortuguese, Is.Not.Empty);
            Assert.That(traditionalChinese, Is.Not.EqualTo(simplifiedChinese));
            Assert.That(europeanPortuguese, Is.Not.EqualTo(brazilianPortuguese));
        }

        [Test]
        public void StartupSelectorsPreferThePlatformLocaleSelector()
        {
            var selectors = LocalizationSettings.StartupLocaleSelectors;
            Assert.That(selectors.OfType<PlatformLocaleSelector>().Count(), Is.EqualTo(1));
            Assert.That(selectors.Last(), Is.TypeOf<SpecificLocaleSelector>());
        }

        [Test]
        public void SavedChoiceWinsOverPlatformLanguages()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            Assert.That(PlatformLocaleSelector.Resolve("ja", locales).Identifier.Code, Is.EqualTo("ja"));
            Assert.That(PlatformLocaleSelector.Resolve("pt-PT", locales).Identifier.Code, Is.EqualTo("pt-PT"));
            Assert.That(PlatformLocaleSelector.Resolve(string.Empty, locales), Is.Not.Null);
        }

        [Test]
        public void LocReturnsEnglishOutsidePlayMode()
        {
            Assert.That(Loc.Get(LocalizationProjectSetup.AppNameKey), Is.EqualTo(PlayerSettings.productName));
            Assert.That(Loc.Has("definitely.not.a.key"), Is.False);
        }

        [Test]
        public void GameNameIsReadFromTheAppNameEntry()
        {
            string name = Loc.Get(LocalizationProjectSetup.AppNameKey);
            Assert.That(name, Is.Not.Empty);
            Assert.That(Loc.Get("tutorial.step.welcome.title"), Is.EqualTo($"Welcome to {name}"));
            Assert.That(Loc.Get("menu.tutorial_prompt.title"), Is.EqualTo($"New to {name}?"));
            Assert.That(Loc.Get("results.banner.mejnun"), Is.EqualTo(name.ToUpperInvariant() + "!"));
            Assert.That(Loc.Get("menu.main.title"), Is.EqualTo(name.ToUpperInvariant()));

            // Translations resolve their own locale's app.name (transliterated).
            var arabic = LocalizationSettings.AvailableLocales.GetLocale("ar");
            Assert.That(arabic, Is.Not.Null);
            string arabicName = LocalizationSettings.StringDatabase.GetLocalizedString(Loc.UiTable,
                LocalizationProjectSetup.AppNameKey, arabic, FallbackBehavior.UseProjectSettings);
            Assert.That(AppNameSource.Resolve(arabic), Is.EqualTo(arabicName));
        }

        [Test]
        public void UxmlLocalizedStringBindingResolvesTheEntry()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/Tests/EditMode/Localization/LocBindingProbe.uxml");
            Assert.That(tree, Is.Not.Null);
            var root = tree.CloneTree();
            var label = root.Q<Label>("probe-label");
            Assert.That(label.TryGetBinding(new BindingId(nameof(Label.text)), out var binding), Is.True);
            var localized = binding as LocalizedString;
            Assert.That(localized, Is.Not.Null);
            Assert.That(localized.TableEntryReference.Key, Is.EqualTo(LocalizationProjectSetup.AppNameKey));
            // Outside Play Mode a binding follows the Editor preview locale,
            // which may be unset on this machine; resolve against English.
            localized.LocaleOverride = LocalizationSettings.ProjectLocale;
            Assert.That(localized.GetLocalizedString(), Is.EqualTo(PlayerSettings.productName));
        }

        [Test]
        public void PlaceholderGuardrailsDetectBrokenTranslations()
        {
            Assert.That(LocalizationValidator.PlaceholdersMatch("{0} cards left", "Il reste {0} cartes", out _), Is.True);
            Assert.That(LocalizationValidator.PlaceholdersMatch("{0} cards left", "Il reste {1} cartes", out _), Is.False);
            Assert.That(LocalizationValidator.PlaceholdersMatch("{0:plural:{} card|{} cards}", "{0:plural:{} carte|{} cartes}", out _), Is.True);
            Assert.That(LocalizationValidator.PlaceholdersMatch("{0:plural:{} card|{} cards}", "{0:plural:{} carte|{} cartes", out _), Is.False);
            Assert.That(LocalizationValidator.IsSheetError("#VALUE!"), Is.True);
            Assert.That(LocalizationValidator.IsSheetError("Loading..."), Is.True);
            Assert.That(LocalizationValidator.IsSmart("Win {0} more games"), Is.True);
            Assert.That(LocalizationValidator.IsSmart("Play"), Is.False);
        }

        [Test]
        public void ArabicPluralGuardrailRequiresSixAlternatives()
        {
            Assert.That(LocalizationValidator.HasSixArabicPluralForms("{0:plural:{} صفر|{} واحد|{} اثنان|{} قليل|{} كثير|{} آخر}"), Is.True);
            Assert.That(LocalizationValidator.HasSixArabicPluralForms("{0:plural:{} واحد|{} جمع}"), Is.False);
            Assert.That(LocalizationValidator.HasSixArabicPluralForms("No plural"), Is.True);
        }

        [TestCase("ar", "١٢٣")]
        [TestCase("fa", "۱۲۳")]
        [TestCase("en", "123")]
        public void LocaleDigitConversionUsesTheExpectedNumerals(string locale, string expected)
        {
            Assert.That(Loc.LocalizeDigits(locale, "123"), Is.EqualTo(expected));
            Assert.That(Loc.LocalizeDigits(locale, ""), Is.Empty);
        }

        [Test]
        public void FormattedStringArgumentsKeepTheirOriginalDigits()
        {
            var previous = LocalizationSettings.SelectedLocale;
            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ar");
                string text = Loc.Format("unlock.requirement.achievement", "123");
                Assert.That(text, Does.Contain("123"));
                Assert.That(text, Does.Not.Contain("١٢٣"));
                Assert.That(Loc.Format("settings.value.percent", 123), Does.Contain("١٢٣"));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
            }
        }

        [TestCase("ar", "١٢٣")]
        [TestCase("fa", "۱۲۳")]
        [TestCase("en", "123")]
        public void NumberUsesTheSelectedLocaleDigits(string locale, string expected)
        {
            var previous = LocalizationSettings.SelectedLocale;
            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(locale);
                Assert.That(Loc.Number(123), Is.EqualTo(expected));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
            }
        }

        [Test]
        public void HandwrittenOverridesMatchTheirStringTableEntries()
        {
            foreach (var translation in HandwrittenTranslationApplier.ReadTranslations())
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(translation.Table);
                var shared = collection?.SharedData.GetEntry(translation.Key);
                var table = collection?.GetTable(translation.Locale) as StringTable;
                Assert.That(shared, Is.Not.Null, translation.Key);
                Assert.That(table?.GetEntry(shared.Id)?.Value, Is.EqualTo(translation.Text), translation.Key + " [" + translation.Locale + "]");
            }
        }

        [Test]
        public void CardInkModeIsSelectedOnlyForRedAndBlackSuits()
        {
            Assert.That(CardInkSettings.ModeForSuit(Mejnun.Core.Suit.Diamonds), Is.EqualTo(CardInkMode.RedToBlue));
            Assert.That(CardInkSettings.ModeForSuit(Mejnun.Core.Suit.Clubs), Is.EqualTo(CardInkMode.BlackToGreen));
            Assert.That(CardInkSettings.ModeForSuit(Mejnun.Core.Suit.Hearts), Is.EqualTo(CardInkMode.None));
            Assert.That(CardInkSettings.ModeForSuit(Mejnun.Core.Suit.Spades), Is.EqualTo(CardInkMode.None));
        }

        [Test]
        public void FrameRateGovernorCapsIdleBatteryAndHighRefreshRates()
        {
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(120, false, false), Is.EqualTo(60));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(50, false, false), Is.EqualTo(50));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(60, false, true), Is.EqualTo(30));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(60, true, false), Is.EqualTo(30));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(24, false, false), Is.EqualTo(24));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(24, false, true), Is.EqualTo(24));
            Assert.That(FrameRateGovernor.ResolveTargetFrameRate(0, false, false), Is.EqualTo(60));
        }

        [Test]
        public void SourceScannerFlagsRawStringsButNotLocCalls()
        {
            var findings = new System.Collections.Generic.List<LocalizationSourceScanner.Finding>();
            LocalizationSourceScanner.ScanCsRawStrings("x.cs", new[]
            {
                "label.text = \"Play\";",
                "label.text = Loc.Get(\"menu.play\");",
                "label.text = $\"{count} / {total}\";",
                "label.text = Loc.Format(\"hud.cards_left\", count);",
                "label.text = \"Halas!\"; // loc-ignore",
                "var b = new Button(() => {}) { text = \"OK\" };",
            }, findings);
            Assert.That(findings.Select(f => f.Line), Is.EqualTo(new[] { 1, 6 }));

            findings.Clear();
            LocalizationSourceScanner.ScanUxmlRawStrings("x.uxml", new[]
            {
                "<ui:Label text=\"Play\" />",
                "<ui:Label text=\"×\" />",
                "<ui:Label class=\"loc-ignore\" text=\"MEJNUN\" />",
                "<ui:Button tooltip=\"Undo the last move\" />",
            }, findings);
            Assert.That(findings.Select(f => f.Line), Is.EqualTo(new[] { 1, 4 }));
        }

        [Test]
        public void CsvReaderHandlesQuotesCommasAndNewlines()
        {
            var rows = LocalizationSourceImporter.ReadCsv(
                "table,key,en,comment\nUI,a.b,\"Hello, \"\"friend\"\"\",note\nUI,c.d,\"Line 1\nLine 2\",\n");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].English, Is.EqualTo("Hello, \"friend\""));
            Assert.That(rows[0].Comment, Is.EqualTo("note"));
            Assert.That(rows[1].English, Is.EqualTo("Line 1\nLine 2"));
        }

        [Test]
        public void PopupSheetPlanWritesKeysMatchesExistingTextAndRetiresRemovedRows()
        {
            var existing = new[]
            {
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.002", English = "Already here" },
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.006", English = "Removed line" },
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.009", English = "Retired line", Retired = true }
            };
            var sheetValues = new List<IList<object>>
            {
                new List<object> { "Deciding Questions" },
                new List<object> { "Already here" },
                new List<object> { "Fresh, bright" }
            };
            var rows = LocalizationSheetImporter.ReadSheetRows(sheetValues, out var hasHeader,
                out var addKeyHeader);
            var plan = LocalizationSheetImporter.PlanTab(rows, existing, "flavor.good.");

            Assert.That(hasHeader, Is.True);
            Assert.That(addKeyHeader, Is.True);
            Assert.That(plan.Lines, Has.Count.EqualTo(2));
            Assert.That(plan.KeyCells[2], Is.EqualTo("flavor.good.002"));
            Assert.That(plan.KeyCells[3], Is.EqualTo("flavor.good.010"));
            Assert.That(plan.RetiredKeys, Is.EquivalentTo(new[] { "flavor.good.006" }));
            Assert.That(plan.RetiredKeys, Does.Not.Contain("flavor.good.009"));
        }

        [Test]
        public void PopupSheetPlanKeepsKeysWhenRowsAreReordered()
        {
            var existing = new[]
            {
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.002", English = "Already here" },
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.010", English = "Fresh, bright" }
            };
            var rows = new[]
            {
                new LocalizationSheetImporter.SheetRow
                    { Row = 2, English = "Fresh, bright", Key = "flavor.good.010" },
                new LocalizationSheetImporter.SheetRow
                    { Row = 3, English = "Already here", Key = "flavor.good.002" }
            };

            var plan = LocalizationSheetImporter.PlanTab(rows, existing, "flavor.good.");

            Assert.That(plan.KeyCells[2], Is.EqualTo("flavor.good.010"));
            Assert.That(plan.KeyCells[3], Is.EqualTo("flavor.good.002"));
            Assert.That(plan.RetiredKeys, Is.Empty);
        }

        [Test]
        public void PopupSheetPlanUpdatesByKeyAndClearsDuplicateRows()
        {
            var existing = new[]
            {
                new LocalizationSheetImporter.ExistingLine
                    { Key = "flavor.good.002", English = "A typo in the old line" }
            };
            var rows = new[]
            {
                new LocalizationSheetImporter.SheetRow
                    { Row = 2, English = "A corrected line", Key = "flavor.good.002" },
                new LocalizationSheetImporter.SheetRow
                    { Row = 3, English = "Fresh line", Key = "flavor.good.011" },
                new LocalizationSheetImporter.SheetRow
                    { Row = 4, English = "Fresh line", Key = "flavor.good.012" }
            };

            var plan = LocalizationSheetImporter.PlanTab(rows, existing, "flavor.good.");

            Assert.That(plan.Lines, Has.Count.EqualTo(2));
            Assert.That(plan.Lines[0].Key, Is.EqualTo("flavor.good.002"));
            Assert.That(plan.Lines[0].English, Is.EqualTo("A corrected line"));
            Assert.That(plan.Lines[1].Key, Is.EqualTo("flavor.good.011"));
            Assert.That(plan.KeyCells[4], Is.Empty);
            Assert.That(plan.RetiredKeys, Is.Empty);
        }

        [Test]
        public void HandwrittenTranslationCsvKeepsSmartStringBracesAndLocale()
        {
            var translation = HandwrittenTranslationApplier.ReadTranslations()
                .Single(item => item.Key == "unlock.requirement.wins" && item.Locale == "ar");
            Assert.That(translation.Table, Is.EqualTo("UI"));
            Assert.That(translation.Text, Does.Contain("{0:plural:"));
            Assert.That(LocalizationValidator.HasSixArabicPluralForms(translation.Text), Is.True);
        }
    }
}
