using System;

namespace Mejnun.Cosmetics
{
    public readonly struct CosmeticUnlockEntry
    {
        public CosmeticUnlockEntry(string id, UnlockRequirement requirement, bool isFelt)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A cosmetic entry must have an id.", nameof(id));
            }

            Id = id;
            Requirement = requirement ?? UnlockRequirement.Free;
            IsFelt = isFelt;
        }

        public string Id { get; }
        public UnlockRequirement Requirement { get; }
        public bool IsFelt { get; }
    }
}