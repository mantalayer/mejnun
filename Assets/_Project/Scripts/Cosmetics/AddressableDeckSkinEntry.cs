using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Mejnun.Cosmetics
{
    [Serializable]
    public sealed class AddressableDeckSkinEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string artist;
        [SerializeField] private UnlockRequirement unlock = new UnlockRequirement();
        [SerializeField] private AssetReferenceT<DeckSkin> asset;

        public AddressableDeckSkinEntry(string skinId, string skinDisplayName, UnlockRequirement requirement,
            AssetReferenceT<DeckSkin> assetReference, string skinArtist = null)
        {
            if (string.IsNullOrWhiteSpace(skinId))
            {
                throw new ArgumentException("An addressable skin must have an id.", nameof(skinId));
            }

            if (assetReference == null)
            {
                throw new ArgumentNullException(nameof(assetReference));
            }

            id = skinId;
            displayName = string.IsNullOrWhiteSpace(skinDisplayName) ? skinId : skinDisplayName;
            artist = skinArtist ?? string.Empty;
            unlock = requirement ?? UnlockRequirement.Free;
            asset = assetReference;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public string Artist => artist;
        public UnlockRequirement Unlock => unlock ?? UnlockRequirement.Free;
        public AssetReferenceT<DeckSkin> Asset => asset;
    }
}