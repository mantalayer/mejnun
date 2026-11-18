using Mejnun.Cosmetics;
using NUnit.Framework;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class ProgressionRulesTests
    {
        [Test]
        public void XpForGameWinMejnunAndStalemateMatchesRules()
        {
            Assert.That(ProgressionRules.XpFor(Solo(false, false, false)), Is.EqualTo(20));
            Assert.That(ProgressionRules.XpFor(Solo(true, false, false)), Is.EqualTo(60));
            Assert.That(ProgressionRules.XpFor(Solo(true, true, false)), Is.EqualTo(80));
            Assert.That(ProgressionRules.XpFor(Solo(false, false, true)), Is.EqualTo(30));
        }

        [Test]
        public void PassAndPlayWithMultipleHumansEarnsParticipationOnly()
        {
            var summary = new MatchSummary(MatchMode.PassAndPlay, 4, 2, true, true, true, false);

            Assert.That(ProgressionRules.XpFor(summary), Is.EqualTo(20));
            var profile = new PlayerProfile();
            profile.RecordMatch(summary);
            Assert.That(profile.Stats.Played, Is.EqualTo(1));
            Assert.That(profile.Stats.Wins, Is.Zero);
            Assert.That(profile.Stats.MejnunWins, Is.Zero);
            Assert.That(profile.Stats.Stalemates, Is.EqualTo(1));
        }

        [Test]
        public void LevelCurveHandlesBoundaries()
        {
            Assert.That(ProgressionRules.LevelForXp(0), Is.EqualTo(1));
            Assert.That(ProgressionRules.LevelForXp(99), Is.EqualTo(1));
            Assert.That(ProgressionRules.LevelForXp(100), Is.EqualTo(2));
            Assert.That(ProgressionRules.LevelForXp(224), Is.EqualTo(2));
            Assert.That(ProgressionRules.LevelForXp(225), Is.EqualTo(3));
            Assert.That(ProgressionRules.LevelProgress(225), Is.EqualTo((0, 150)));
            Assert.That(ProgressionRules.LevelProgress(-1), Is.EqualTo((0, 100)));
        }

        [Test]
        public void AchievementsAreReportedOnceWhenTheirConditionsAreMet()
        {
            var profile = new PlayerProfile();
            var firstWin = Solo(true, true, false);
            profile.RecordMatch(firstWin);
            var achievements = AchievementTracker.Evaluate(profile, firstWin);
            CollectionAssert.AreEquivalent(new[]
            {
                AchievementIds.FirstGame,
                AchievementIds.FirstWin,
                AchievementIds.FirstMejnun,
                AchievementIds.BeatHardBot
            }, achievements);

            for (var index = 0; index < achievements.Count; index++)
            {
                profile.AddAchievement(achievements[index]);
            }

            profile.RecordMatch(Solo(false, false, false));
            Assert.That(AchievementTracker.Evaluate(profile, firstWin), Is.Empty);

            for (var index = 1; index < 10; index++)
            {
                profile.RecordMatch(Solo(true, false, false));
                var newlyEarned = AchievementTracker.Evaluate(profile, Solo(true, false, false));
                for (var achievementIndex = 0; achievementIndex < newlyEarned.Count; achievementIndex++)
                {
                    profile.AddAchievement(newlyEarned[achievementIndex]);
                }
            }

            Assert.That(profile.HasAchievement(AchievementIds.TenWins), Is.True);
            var fullTable = new MatchSummary(MatchMode.PassAndPlay, 6, 6, false, false, false, false);
            CollectionAssert.Contains(AchievementTracker.Evaluate(profile, fullTable), AchievementIds.FullTable);
        }

        private static MatchSummary Solo(bool win, bool mejnun, bool stalemate)
        {
            return new MatchSummary(MatchMode.Solo, 2, 1, win, mejnun, stalemate, win && mejnun);
        }
    }
}