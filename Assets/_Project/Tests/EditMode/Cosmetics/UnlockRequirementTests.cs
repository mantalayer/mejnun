using Mejnun.Cosmetics;
using NUnit.Framework;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class UnlockRequirementTests
    {
        [Test]
        public void UnlockRequirementsReportCountableAndNonCountableProgress()
        {
            var profile = new PlayerProfile();
            var free = new UnlockRequirement(UnlockKind.Free).Evaluate(profile);
            var level = new UnlockRequirement(UnlockKind.Level, 2).Evaluate(profile);
            var wins = new UnlockRequirement(UnlockKind.Wins, 2).Evaluate(profile);
            var achievement = new UnlockRequirement(UnlockKind.Achievement, achievementId: AchievementIds.FirstWin)
                .Evaluate(profile);
            var purchase = new UnlockRequirement(UnlockKind.Purchase).Evaluate(profile);

            Assert.That(free.Unlocked, Is.True);
            Assert.That(free.IsCountable, Is.False);
            Assert.That(level.Current, Is.EqualTo(1));
            Assert.That(level.Required, Is.EqualTo(2));
            Assert.That(level.IsCountable, Is.True);
            Assert.That(wins.Current, Is.Zero);
            Assert.That(wins.Required, Is.EqualTo(2));
            Assert.That(achievement.Current, Is.Zero);
            Assert.That(achievement.Required, Is.EqualTo(1));
            Assert.That(achievement.IsCountable, Is.True);
            Assert.That(purchase.Unlocked, Is.False);
            Assert.That(purchase.IsCountable, Is.False);
        }

        [Test]
        public void UnlockRequirementDescriptionsArePlayerFacing()
        {
            Assert.That(new UnlockRequirement(UnlockKind.Level, 5).Describe(default), Is.EqualTo("Reach level 5"));
            Assert.That(new UnlockRequirement(UnlockKind.Wins, 3).Describe(default), Is.EqualTo("Win 3 games"));
            Assert.That(new UnlockRequirement(UnlockKind.Achievement,
                achievementId: AchievementIds.FirstMejnun).Describe(default), Is.EqualTo("Earn First MEJNUN"));
            Assert.That(new UnlockRequirement(UnlockKind.Purchase).Describe(default), Is.EqualTo("Purchase required"));
        }

        [Test]
        public void UnlockRequirementRejectsInvalidTargetsAndAchievementIds()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new UnlockRequirement(UnlockKind.Level, 0));
            Assert.Throws<System.ArgumentException>(() =>
                new UnlockRequirement(UnlockKind.Achievement, achievementId: string.Empty));
        }
    }
}