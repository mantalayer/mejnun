using System;
using System.IO;
using Mejnun.Cosmetics;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class ProfileStore
    {
        private const long MaximumProfileBytes = 2 * 1024 * 1024;
        private readonly string profilePath;

        public ProfileStore()
            : this(Path.Combine(Application.persistentDataPath, "mejnun_profile.json"))
        {
        }

        public ProfileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile file path is required.", nameof(path));
            }

            profilePath = path;
        }

        public string ProfilePath => profilePath;

        public PlayerProfile Load()
        {
            if (!File.Exists(profilePath))
            {
                return new PlayerProfile();
            }

            try
            {
                var length = new FileInfo(profilePath).Length;
                if (length <= 0 || length > MaximumProfileBytes)
                {
                    throw new InvalidDataException("Invalid player profile size.");
                }

                return ProfileSerializer.FromJson(File.ReadAllText(profilePath));
            }
            catch (Exception exception)
            {
                BackupCorruptFile();
                Debug.LogWarning("Could not load the Mejnun profile; using a fresh profile: " + exception.Message);
                var freshProfile = new PlayerProfile();
                try
                {
                    Save(freshProfile);
                }
                catch (Exception saveException)
                {
                    Debug.LogWarning("Could not replace the corrupt Mejnun profile: " + saveException.Message);
                }

                return freshProfile;
            }
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var directory = Path.GetDirectoryName(profilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = profilePath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, ProfileSerializer.ToJson(profile));
                if (File.Exists(profilePath))
                {
                    File.Replace(temporaryPath, profilePath, null);
                }
                else
                {
                    File.Move(temporaryPath, profilePath);
                }
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }

        private void BackupCorruptFile()
        {
            try
            {
                File.Copy(profilePath, profilePath + ".bak", true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not back up the corrupt Mejnun profile: " + exception.Message);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}