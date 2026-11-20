using System;
using System.IO;
using UnityEngine;

namespace Mejnun.Platform.Editor
{
    [Serializable]
    internal sealed class SteamUploadLocalSettings
    {
        private const string FileName = "MejnunSteamUpload.json";

        public string ContentBuilderPath;
        public string BuilderAccount;

        public static string FilePath
        {
            get
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, "UserSettings", FileName);
            }
        }

        public static SteamUploadLocalSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<SteamUploadLocalSettings>(File.ReadAllText(FilePath)) ?? new SteamUploadLocalSettings();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Steam Upload] Could not read local settings: {exception.Message}");
            }

            return new SteamUploadLocalSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
        }
    }
}