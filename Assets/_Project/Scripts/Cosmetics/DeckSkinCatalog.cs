using System;
using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [CreateAssetMenu(menuName = "Mejnun/Deck Skin Catalog")]
    public sealed class DeckSkinCatalog : ScriptableObject
    {
        [SerializeField] private List<DeckSkin> skins = new List<DeckSkin>();
        [SerializeField] private List<AddressableDeckSkinEntry> addressableSkins =
            new List<AddressableDeckSkinEntry>();

        private readonly Dictionary<string, DeckSkin> loadedAddressableSkins =
            new Dictionary<string, DeckSkin>(StringComparer.Ordinal);

        public IReadOnlyList<DeckSkin> Skins => skins;
        public IReadOnlyList<AddressableDeckSkinEntry> AddressableSkins => addressableSkins;

        public string[] GetSkinIds()
        {
            var ids = new List<string>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            AppendSkinIds(skins, ids, uniqueIds);
            if (addressableSkins != null)
            {
                for (var index = 0; index < addressableSkins.Count; index++)
                {
                    var entry = addressableSkins[index];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Id) && uniqueIds.Add(entry.Id))
                    {
                        ids.Add(entry.Id);
                    }
                }
            }

            return ids.ToArray();
        }

        public string[] GetAvailableSkinIds()
        {
            var ids = new List<string>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            AppendSkinIds(skins, ids, uniqueIds);
            foreach (var skin in loadedAddressableSkins.Values)
            {
                AppendSkinIds(new[] { skin }, ids, uniqueIds);
            }

            return ids.ToArray();
        }

        public IReadOnlyList<DeckSkinInfo> GetEntries()
        {
            var entries = new List<DeckSkinInfo>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (skins != null)
            {
                for (var index = 0; index < skins.Count; index++)
                {
                    var skin = skins[index];
                    if (skin != null && !string.IsNullOrWhiteSpace(skin.Id) && ids.Add(skin.Id))
                    {
                        entries.Add(new DeckSkinInfo(skin.Id, skin.DisplayName, skin.Unlock, false, skin,
                            skin.Artist));
                    }
                }
            }

            if (addressableSkins != null)
            {
                for (var index = 0; index < addressableSkins.Count; index++)
                {
                    var entry = addressableSkins[index];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Id) && ids.Add(entry.Id))
                    {
                        loadedAddressableSkins.TryGetValue(entry.Id, out var loadedSkin);
                        var artist = string.IsNullOrWhiteSpace(entry.Artist) && loadedSkin != null
                            ? loadedSkin.Artist
                            : entry.Artist;
                        entries.Add(new DeckSkinInfo(entry.Id, entry.DisplayName, entry.Unlock, true, loadedSkin,
                            artist));
                    }
                }
            }

            return entries.AsReadOnly();
        }

        public DeckSkin Resolve(string faceId, int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= Card.DeckCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deckIndex), deckIndex,
                    "Deck index must be zero or one.");
            }

            var requested = Find(DeckFaceId.GetSkinId(faceId));
            if (requested != null)
            {
                return requested;
            }

            return Find(deckIndex == 0 ? DeckFaceAssigner.ClassicFace : DeckFaceAssigner.ClassicIvoryFace);
        }

        public Sprite ResolveBack(string faceId, int deckIndex)
        {
            var skin = Resolve(faceId, deckIndex);
            return skin == null ? null : skin.GetBack(DeckFaceId.GetBackId(faceId));
        }

        public void Configure(IReadOnlyList<DeckSkin> deckSkins)
        {
            if (deckSkins == null)
            {
                throw new ArgumentNullException(nameof(deckSkins));
            }

            var configuredSkins = new List<DeckSkin>(deckSkins.Count);
            for (var index = 0; index < deckSkins.Count; index++)
            {
                if (deckSkins[index] == null)
                {
                    throw new ArgumentException("A skin catalog cannot contain a missing skin.", nameof(deckSkins));
                }

                configuredSkins.Add(deckSkins[index]);
            }

            skins = configuredSkins;
        }

        public void ConfigureAddressableSkins(IReadOnlyList<AddressableDeckSkinEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var configuredEntries = new List<AddressableDeckSkinEntry>(entries.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Asset == null ||
                    !ids.Add(entry.Id) || FindBuiltIn(entry.Id) != null)
                {
                    throw new ArgumentException("Addressable skin entries must have unique, non-built-in ids and assets.",
                        nameof(entries));
                }

                configuredEntries.Add(entry);
            }

            addressableSkins = configuredEntries;
            loadedAddressableSkins.Clear();
        }

        public bool IsAddressableSkin(string skinId)
        {
            return FindAddressableEntry(skinId) != null;
        }

        public bool IsAddressableSkinLoaded(string skinId)
        {
            return loadedAddressableSkins.ContainsKey(skinId);
        }

        public AddressableDeckSkinEntry FindAddressableEntry(string skinId)
        {
            if (addressableSkins == null || string.IsNullOrWhiteSpace(skinId))
            {
                return null;
            }

            for (var index = 0; index < addressableSkins.Count; index++)
            {
                var entry = addressableSkins[index];
                if (entry != null && string.Equals(entry.Id, skinId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public void RegisterLoaded(DeckSkin skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            if (FindAddressableEntry(skin.Id) == null)
            {
                throw new ArgumentException("Loaded skin is not registered as an addressable entry.", nameof(skin));
            }

            loadedAddressableSkins[skin.Id] = skin;
        }

        public void UnregisterLoaded(string skinId)
        {
            if (!string.IsNullOrWhiteSpace(skinId))
            {
                loadedAddressableSkins.Remove(skinId);
            }
        }

        private DeckSkin Find(string faceId)
        {
            if (string.IsNullOrEmpty(faceId))
            {
                return null;
            }

            if (loadedAddressableSkins.TryGetValue(faceId, out var loadedSkin))
            {
                return loadedSkin;
            }

            return FindBuiltIn(faceId);
        }

        private DeckSkin FindBuiltIn(string faceId)
        {
            if (skins == null || string.IsNullOrEmpty(faceId))
            {
                return null;
            }

            for (var index = 0; index < skins.Count; index++)
            {
                var skin = skins[index];
                if (skin != null && string.Equals(skin.Id, faceId, StringComparison.Ordinal))
                {
                    return skin;
                }
            }

            return null;
        }

        private static void AppendSkinIds(IReadOnlyList<DeckSkin> source, List<string> ids,
            HashSet<string> uniqueIds)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var skin = source[index];
                if (skin == null || string.IsNullOrWhiteSpace(skin.Id) || !uniqueIds.Add(skin.Id))
                {
                    continue;
                }

                ids.Add(skin.Id);
                var backIds = skin.BackIds;
                for (var backIndex = 0; backIndex < backIds.Length; backIndex++)
                {
                    if (string.IsNullOrWhiteSpace(backIds[backIndex]) || backIds[backIndex].IndexOf(':') >= 0)
                    {
                        continue;
                    }

                    var variantId = DeckFaceId.Compose(skin.Id, backIds[backIndex]);
                    if (uniqueIds.Add(variantId))
                    {
                        ids.Add(variantId);
                    }
                }
            }
        }
    }
}