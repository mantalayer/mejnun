using Mejnun.Cosmetics;
using NUnit.Framework;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class UnlockEvaluatorTests
    {
        [Test]
        public void OwnershipAndNewItemsFollowProfileProgressAndSeenSet()
        {
            var profile = new PlayerProfile();
            var entries = new[]
            {
                new CosmeticUnlockEntry("classic", new UnlockRequirement(UnlockKind.Free), false),
                new CosmeticUnlockEntry("level_two", new UnlockRequirement(UnlockKind.Level, 2), false),
                new CosmeticUnlockEntry("one_win", new UnlockRequirement(UnlockKind.Wins, 1), true),
                new CosmeticUnlockEntry("purchase", new UnlockRequirement(UnlockKind.Purchase), false)
            };
            var evaluator = new UnlockEvaluator(profile, entries);

            CollectionAssert.AreEqual(new[] { "classic" }, evaluator.OwnedFaceIds());
            CollectionAssert.IsEmpty(evaluator.OwnedFeltIds());
            Assert.That(profile.HasSeen("classic"), Is.True);
            Assert.That(evaluator.NewItems(), Is.Empty);

            profile.AddXp(100);
            profile.RecordMatch(new MatchSummary(MatchMode.Solo, 2, 1, true, false, false, false));

            CollectionAssert.AreEqual(new[] { "classic", "level_two" }, evaluator.OwnedFaceIds());
            CollectionAssert.AreEqual(new[] { "one_win" }, evaluator.OwnedFeltIds());
            CollectionAssert.AreEquivalent(new[] { "level_two", "one_win" }, evaluator.NewItems());

            profile.MarkSeen("level_two");
            profile.GrantItem("purchase");
            Assert.That(evaluator.IsOwned("purchase", entries[3].Requirement), Is.True);
            CollectionAssert.AreEquivalent(new[] { "one_win", "purchase" }, evaluator.NewItems());
        }
    }
}