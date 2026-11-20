using Mejnun.Localization;
using NUnit.Framework;
using UnityEngine.Localization.Settings;

// No namespace: applies to every PlayMode test in this assembly. The startup
// locale follows the machine's language (PlatformLocaleSelector), so pin the
// tests to English, the language their text assertions are written in.
[SetUpFixture]
public sealed class EnglishLocaleSetUpFixture
{
    [OneTimeSetUp]
    public void UseEnglish()
    {
        if (!LocalizationSettings.HasSettings)
            return;

        var english = LocalizationSettings.AvailableLocales.GetLocale(Loc.ProjectLocaleCode);
        if (english != null)
            LocalizationSettings.SelectedLocale = english;
    }
}
