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

            var candidates = new List<string>(facePool.Count + 2);
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

            return new[] { candidates[0], candidates[1] };
        }

        private static void AddDistinct(List<string> faces, string face)
        {
            if (face == null)
            {
                throw new ArgumentException("Face ids cannot be null.", nameof(face));
            }

            for (var index = 0; index < faces.Count; index++)
            {
                if (string.Equals(faces[index], face, StringComparison.Ordinal))
                {
                    return;
                }
            }

            faces.Add(face);
        }
    }
}