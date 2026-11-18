using System;
using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mejnun.Cosmetics
{
    public sealed class DeckSkinLoader : MonoBehaviour
    {
        [SerializeField] private DeckSkinCatalog catalog;

        private readonly Dictionary<string, AsyncOperationHandle<DeckSkin>> handles =
            new Dictionary<string, AsyncOperationHandle<DeckSkin>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action<DeckSkin>>> pendingCallbacks =
            new Dictionary<string, List<Action<DeckSkin>>>(StringComparer.Ordinal);
        private readonly HashSet<string> loadAttempted = new HashSet<string>(StringComparer.Ordinal);

        public void Configure(DeckSkinCatalog skinCatalog)
        {
            catalog = skinCatalog;
        }

        public bool Preload(IReadOnlyList<string> faceIds, Action completed)
        {
            if (catalog == null || faceIds == null)
            {
                return false;
            }

            var entries = new List<AddressableDeckSkinEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < faceIds.Count; index++)
            {
                var faceId = faceIds[index];
                if (string.IsNullOrWhiteSpace(faceId))
                {
                    continue;
                }

                var skinId = DeckFaceId.GetSkinId(faceId);
                var entry = catalog.FindAddressableEntry(skinId);
                if (entry != null && !catalog.IsAddressableSkinLoaded(skinId) && ids.Add(skinId))
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                return false;
            }

            var remaining = entries.Count;
            for (var index = 0; index < entries.Count; index++)
            {
                Load(entries[index].Id, _ =>
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        completed?.Invoke();
                    }
                });
            }

            return true;
        }

        public void Load(string skinId, Action<DeckSkin> completed)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(skinId))
            {
                completed?.Invoke(null);
                return;
            }

            if (catalog.IsAddressableSkinLoaded(skinId))
            {
                completed?.Invoke(catalog.Resolve(skinId, 0));
                return;
            }

            var entry = catalog.FindAddressableEntry(skinId);
            if (entry == null || entry.Asset == null)
            {
                completed?.Invoke(null);
                return;
            }

            if (pendingCallbacks.TryGetValue(skinId, out var callbacks))
            {
                callbacks.Add(completed);
                return;
            }

            if (loadAttempted.Contains(skinId))
            {
                completed?.Invoke(null);
                return;
            }

            loadAttempted.Add(skinId);
            callbacks = new List<Action<DeckSkin>> { completed };
            pendingCallbacks.Add(skinId, callbacks);
            try
            {
                var handle = entry.Asset.LoadAssetAsync();
                handles.Add(skinId, handle);
                handle.Completed += operation => CompleteLoad(skinId, operation);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not start loading deck skin " + skinId + ": " + exception.Message);
                CompleteLoad(skinId, default);
            }
        }

        private void OnDestroy()
        {
            foreach (var skinId in handles.Keys)
            {
                catalog?.UnregisterLoaded(skinId);
            }

            foreach (var handle in handles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            handles.Clear();
            pendingCallbacks.Clear();
        }

        private void CompleteLoad(string skinId, AsyncOperationHandle<DeckSkin> operation)
        {
            DeckSkin loadedSkin = null;
            if (operation.IsValid() && operation.Status == AsyncOperationStatus.Succeeded && operation.Result != null)
            {
                loadedSkin = operation.Result;
                catalog.RegisterLoaded(loadedSkin);
            }
            else
            {
                Debug.LogWarning("Addressable deck skin could not be loaded: " + skinId + ". Using the base face.");
            }

            if (!pendingCallbacks.TryGetValue(skinId, out var callbacks))
            {
                return;
            }

            pendingCallbacks.Remove(skinId);
            for (var index = 0; index < callbacks.Count; index++)
            {
                callbacks[index]?.Invoke(loadedSkin);
            }
        }
    }
}