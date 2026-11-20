using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mejnun.Platform
{
    public sealed class JsonFileSaveStorage : ISaveStorage
    {
        private readonly string m_directory;

        public static JsonFileSaveStorage Instance { get; } = new JsonFileSaveStorage();

        public JsonFileSaveStorage() : this(Application.persistentDataPath)
        {
        }

        internal JsonFileSaveStorage(string directory)
        {
            m_directory = Path.GetFullPath(directory);
        }

        public bool FileExists(string file)
        {
            return File.Exists(GetFilePath(file));
        }

        public bool KeyExists(string key, string file)
        {
            ValidateKey(key);
            StorageFile storageFile = ReadFile(file);
            return FindEntry(storageFile, key) != null;
        }

        public void Save<T>(string key, T value, string file)
        {
            ValidateKey(key);
            string path = GetFilePath(file);
            StorageFile storageFile = ReadFilePath(path);
            StorageEntry entry = FindEntry(storageFile, key);
            string serializedValue = JsonUtility.ToJson(new ValueWrapper<T> { value = value });
            if (entry == null)
            {
                storageFile.entries.Add(new StorageEntry { key = key, value = serializedValue });
            }
            else
            {
                entry.value = serializedValue;
            }

            WriteFile(path, storageFile);
        }

        public T Load<T>(string key, string file)
        {
            ValidateKey(key);
            StorageEntry entry = FindEntry(ReadFile(file), key);
            if (entry == null)
            {
                throw new KeyNotFoundException($"Save key '{key}' was not found in '{file}'.");
            }

            ValueWrapper<T> wrapper = JsonUtility.FromJson<ValueWrapper<T>>(entry.value);
            return wrapper == null ? default : wrapper.value;
        }

        public T Load<T>(string key, string file, T defaultValue)
        {
            ValidateKey(key);
            StorageEntry entry = FindEntry(ReadFile(file), key);
            if (entry == null)
            {
                return defaultValue;
            }

            ValueWrapper<T> wrapper = JsonUtility.FromJson<ValueWrapper<T>>(entry.value);
            return wrapper == null ? default : wrapper.value;
        }

        public void LoadInto<T>(string key, string file, T obj) where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            ValidateKey(key);
            StorageEntry entry = FindEntry(ReadFile(file), key);
            if (entry == null)
            {
                throw new KeyNotFoundException($"Save key '{key}' was not found in '{file}'.");
            }

            ValueWrapper<T> wrapper = JsonUtility.FromJson<ValueWrapper<T>>(entry.value);
            if (wrapper?.value != null)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(wrapper.value), obj);
            }
        }

        public void DeleteKey(string key, string file)
        {
            ValidateKey(key);
            string path = GetFilePath(file);
            StorageFile storageFile = ReadFilePath(path);
            StorageEntry entry = FindEntry(storageFile, key);
            if (entry == null)
            {
                return;
            }

            storageFile.entries.Remove(entry);
            WriteFile(path, storageFile);
        }

        public void DeleteFile(string file)
        {
            string path = GetFilePath(file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private StorageFile ReadFile(string file)
        {
            return ReadFilePath(GetFilePath(file));
        }

        private static StorageFile ReadFilePath(string path)
        {
            if (!File.Exists(path))
            {
                return new StorageFile();
            }

            StorageFile storageFile = JsonUtility.FromJson<StorageFile>(File.ReadAllText(path));
            if (storageFile == null)
            {
                throw new InvalidDataException($"Save file '{path}' contains invalid JSON.");
            }

            storageFile.entries ??= new List<StorageEntry>();
            return storageFile;
        }

        private static StorageEntry FindEntry(StorageFile storageFile, string key)
        {
            for (int index = 0; index < storageFile.entries.Count; index++)
            {
                if (string.Equals(storageFile.entries[index].key, key, StringComparison.Ordinal))
                {
                    return storageFile.entries[index];
                }
            }

            return null;
        }

        private void WriteFile(string path, StorageFile storageFile)
        {
            Directory.CreateDirectory(m_directory);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(storageFile, true));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private string GetFilePath(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || file == "." || file == ".." ||
                file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.IndexOf('/') >= 0 ||
                file.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("A valid save file name is required.", nameof(file));
            }

            return Path.Combine(m_directory, Path.ChangeExtension(file, ".json"));
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A save key is required.", nameof(key));
            }
        }

        [Serializable]
        private sealed class StorageFile
        {
            public List<StorageEntry> entries = new List<StorageEntry>();
        }

        [Serializable]
        private sealed class StorageEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private sealed class ValueWrapper<T>
        {
            public T value;
        }
    }
}