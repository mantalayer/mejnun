using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [CreateAssetMenu(menuName = "Mejnun/Cosmetic Catalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        [SerializeField] private DeckSkinCatalog decks;
        [SerializeField] private TableFeltCatalog felts;

        public DeckSkinCatalog Decks => decks;
        public TableFeltCatalog Felts => felts;

        public void Configure(DeckSkinCatalog deckCatalog, TableFeltCatalog feltCatalog)
        {
            if (deckCatalog == null)
            {
                throw new ArgumentNullException(nameof(deckCatalog));
            }

            if (feltCatalog == null)
            {
                throw new ArgumentNullException(nameof(feltCatalog));
            }

            decks = deckCatalog;
            felts = feltCatalog;
        }

        public IReadOnlyList<CosmeticUnlockEntry> GetUnlockEntries()
        {
            var entries = new List<CosmeticUnlockEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (decks != null && decks.Skins != null)
            {
                for (var index = 0; index < decks.Skins.Count; index++)
                {
                    var skin = decks.Skins[index];
                    if (skin != null && !string.IsNullOrWhiteSpace(skin.Id) && ids.Add(skin.Id))
                    {
                        entries.Add(new CosmeticUnlockEntry(skin.Id, skin.Unlock, false));
                    }
                }
            }

            if (decks != null && decks.AddressableSkins != null)
            {
                for (var index = 0; index < decks.AddressableSkins.Count; index++)
                {
                    var skin = decks.AddressableSkins[index];
                    if (skin != null && !string.IsNullOrWhiteSpace(skin.Id) && ids.Add(skin.Id))
                    {
                        entries.Add(new CosmeticUnlockEntry(skin.Id, skin.Unlock, false));
                    }
                }
            }

            if (felts != null && felts.Felts != null)
            {
                for (var index = 0; index < felts.Felts.Count; index++)
                {
                    var felt = felts.Felts[index];
                    if (felt != null && !string.IsNullOrWhiteSpace(felt.Id) && ids.Add(felt.Id))
                    {
                        entries.Add(new CosmeticUnlockEntry(felt.Id, felt.Unlock, true));
                    }
                }
            }

            return entries.AsReadOnly();
        }
    }
}