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

        public DeckSkin Resolve(string faceId, int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= Card.DeckCount)
            {
                throw new ArgumentOutOfRangeException(nameof(deckIndex), deckIndex,
                    "Deck index must be zero or one.");
            }

            var requested = Find(faceId);
            if (requested != null)
            {
                return requested;
            }

            return Find(deckIndex == 0 ? DeckFaceAssigner.ClassicFace : DeckFaceAssigner.ClassicIvoryFace);
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