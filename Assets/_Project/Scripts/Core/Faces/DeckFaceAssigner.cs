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