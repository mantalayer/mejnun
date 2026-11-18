using System;
using System.Collections.Generic;

namespace Mejnun.Core
{
    public static class DeckFaceAssigner
    {
        public const string ClassicFace = "classic";
        public const string ClassicIvoryFace = "classic_ivory";

        public static string[] Assign(IReadOnlyList<string> facePool, Pcg32 rng)
        {
            if (facePool == null)
            {
                throw new ArgumentNullException(nameof(facePool));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var candidates = new List<FaceChoiceGroup>(facePool.Count + 2);
            for (var index = 0; index < facePool.Count; index++)
            {
                AddDistinct(candidates, facePool[index]);
            }

            if (candidates.Count < 2)
            {
                AddDistinct(candidates, ClassicFace);
                AddDistinct(candidates, ClassicIvoryFace);
            }

            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var otherIndex = rng.NextInt(index + 1);
                var face = candidates[index];
                candidates[index] = candidates[otherIndex];
                candidates[otherIndex] = face;
            }

            return new[] { SelectFace(candidates[0], rng), SelectFace(candidates[1], rng) };
        }

        public static string[] AssignFromPicks(IReadOnlyList<string> picks,
            IReadOnlyList<string> randomPool, Pcg32 rng)
        {
            if (picks == null)
            {
                throw new ArgumentNullException(nameof(picks));
            }

            if (picks.Count != Card.DeckCount)
            {
                throw new ArgumentException("Exactly two deck picks are required.", nameof(picks));
            }

            if (randomPool == null)
            {
                throw new ArgumentNullException(nameof(randomPool));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var assigned = new string[Card.DeckCount];
            var usedSkinIds = new HashSet<string>(StringComparer.Ordinal);
            for (var slot = 0; slot < assigned.Length; slot++)
            {
                var pick = picks[slot];
                if (pick == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(pick))
                {
                    throw new ArgumentException("Deck picks must be non-empty ids or null for Random.",
                        nameof(picks));
                }

                var skinId = DeckFaceId.GetSkinId(pick);
                if (!usedSkinIds.Add(skinId))
                {
                    assigned[slot] = null;
                    continue;
                }

                assigned[slot] = pick;
            }

            var candidates = new List<FaceChoiceGroup>(randomPool.Count + 2);
            for (var index = 0; index < randomPool.Count; index++)
            {
                AddDistinct(candidates, randomPool[index]);
            }

            for (var slot = 0; slot < assigned.Length; slot++)
            {
                if (assigned[slot] != null)
                {
                    continue;
                }

                var available = AvailableGroups(candidates, usedSkinIds);
                if (available.Count == 0)
                {
                    var fallback = new List<FaceChoiceGroup>(2);
                    AddDistinct(fallback, ClassicFace);
                    AddDistinct(fallback, ClassicIvoryFace);
                    available = AvailableGroups(fallback, usedSkinIds);
                }

                var choice = available[rng.NextInt(available.Count)];
                assigned[slot] = SelectFace(choice, rng);
                usedSkinIds.Add(choice.SkinId);
            }

            return assigned;
        }

        private static List<FaceChoiceGroup> AvailableGroups(List<FaceChoiceGroup> candidates,
            HashSet<string> usedSkinIds)
        {
            var available = new List<FaceChoiceGroup>(candidates.Count);
            for (var index = 0; index < candidates.Count; index++)
            {
                if (!usedSkinIds.Contains(candidates[index].SkinId))
                {
                    available.Add(candidates[index]);
                }
            }

            return available;
        }

        private static string SelectFace(FaceChoiceGroup group, Pcg32 rng)
        {
            if (group.Choices.Count == 1)
            {
                return group.Choices[0];
            }

            return group.Choices[rng.NextInt(group.Choices.Count)];
        }

        private static void AddDistinct(List<FaceChoiceGroup> faces, string face)
        {
            if (face == null)
            {
                throw new ArgumentException("Face ids cannot be null.", nameof(face));
            }

            var skinId = DeckFaceId.GetSkinId(face);
            for (var index = 0; index < faces.Count; index++)
            {
                if (string.Equals(faces[index].SkinId, skinId, StringComparison.Ordinal))
                {
                    for (var choiceIndex = 0; choiceIndex < faces[index].Choices.Count; choiceIndex++)
                    {
                        if (string.Equals(faces[index].Choices[choiceIndex], face, StringComparison.Ordinal))
                        {
                            return;
                        }
                    }

                    faces[index].Choices.Add(face);
                    return;
                }
            }

            faces.Add(new FaceChoiceGroup(skinId, face));
        }

        private sealed class FaceChoiceGroup
        {
            public string SkinId { get; }
            public List<string> Choices { get; }

            public FaceChoiceGroup(string skinId, string face)
            {
                SkinId = skinId;
                Choices = new List<string> { face };
            }
        }
    }
}