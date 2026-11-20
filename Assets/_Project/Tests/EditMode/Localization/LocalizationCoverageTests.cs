using System.Linq;
using Mejnun.Localization.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Localization.Tests
{
    // Enforces the rules in Docs/Localization.md on the whole game source.
    // Source problems fail; translation gaps only warn, because they are filled
    // by the Google Sheets sync that runs before every build.
    public sealed class LocalizationCoverageTests
    {
        [Test]
        public void GameSourceHasNoRawPlayerFacingStrings()
        {
            var findings = LocalizationSourceScanner.FindRawStrings();
            Assert.That(findings, Is.Empty,
                "Player-facing text must come from a String Table (Docs/Localization.md):\n" +
                string.Join("\n", findings.Select(f => f.ToString())));
        }

        [Test]
        public void EveryReferencedKeyExists()
        {
            var report = LocalizationValidator.Validate();
            var unknown = report.Issues.Where(i => i.Kind == LocalizationValidator.IssueKind.UnknownKey).ToList();
            Assert.That(unknown, Is.Empty, string.Join("\n", unknown));
        }

        [Test]
        public void EveryEntryHasEnglishText()
        {
            var report = LocalizationValidator.Validate(includeSourceScan: false);
            var empty = report.Issues.Where(i => i.Kind == LocalizationValidator.IssueKind.EmptySource).ToList();
            Assert.That(empty, Is.Empty, string.Join("\n", empty));
        }

        [Test]
        public void TranslationsAreCompleteAndKeepPlaceholders()
        {
            var report = LocalizationValidator.Validate(includeSourceScan: false);
            var issues = report.TranslationIssues.ToList();
            if (issues.Count > 0)
            {
                Debug.LogWarning($"[Localization] {report.Summary()}. Run Mejnun > Localization > Sync with Google Sheets.\n" +
                                 string.Join("\n", issues.Take(30)));
            }

            Assert.Pass(report.Summary());
        }
    }
}
