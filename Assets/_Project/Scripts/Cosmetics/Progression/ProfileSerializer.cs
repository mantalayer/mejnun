using System;
using System.IO;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    public static class ProfileSerializer
    {
        public static string ToJson(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            profile.MigrateAndNormalize();
            return JsonUtility.ToJson(profile, true);
        }

        public static PlayerProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("Player profile JSON is empty.");
            }

            var trimmed = json.Trim();
            if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            {
                throw new InvalidDataException("Player profile JSON must contain an object.");
            }

            try
            {
                var profile = JsonUtility.FromJson<PlayerProfile>(trimmed);
                if (profile == null)
                {
                    throw new InvalidDataException("Player profile JSON did not contain a profile.");
                }

                profile.MigrateAndNormalize();
                return profile;
            }
            catch (Exception exception) when (!(exception is InvalidDataException))
            {
                throw new InvalidDataException("Could not read the player profile JSON.", exception);
            }
        }
    }
}