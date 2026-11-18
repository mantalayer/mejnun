using System;

namespace Mejnun.Core
{
    public static class DeckFaceId
    {
        public static string Compose(string skinId, string backId)
        {
            if (string.IsNullOrWhiteSpace(skinId))
            {
                throw new ArgumentException("A deck face id must have a skin id.", nameof(skinId));
            }

            if (string.IsNullOrWhiteSpace(backId))
            {
                return skinId;
            }

            if (skinId.IndexOf(':') >= 0 || backId.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Skin and back ids cannot contain a colon.");
            }

            return skinId + ":" + backId;
        }

        public static string GetSkinId(string faceId)
        {
            var separator = string.IsNullOrEmpty(faceId) ? -1 : faceId.IndexOf(':');
            return separator > 0 && separator < faceId.Length - 1
                ? faceId.Substring(0, separator)
                : faceId;
        }

        public static string GetBackId(string faceId)
        {
            var separator = string.IsNullOrEmpty(faceId) ? -1 : faceId.IndexOf(':');
            return separator > 0 && separator < faceId.Length - 1
                ? faceId.Substring(separator + 1)
                : null;
        }
    }
}