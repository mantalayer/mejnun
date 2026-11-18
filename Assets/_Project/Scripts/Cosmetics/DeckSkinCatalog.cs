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

        public IReadOnlyList<DeckSkin> Skins => skins;

        public string[] GetSkinIds()
        {
            if (skins == null)
            {
                return Array.Empty<string>();
            }

            var ids = new List<string>(skins.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < skins.Count; index++)
            {
                var skin = skins[index];
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

            return ids.ToArray();
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

        private DeckSkin Find(string faceId)
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
    }
}