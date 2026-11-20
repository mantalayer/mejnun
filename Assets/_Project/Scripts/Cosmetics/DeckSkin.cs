using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Localization;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [Serializable]
    public sealed class DeckSkinBack
    {
        [SerializeField] private string id;
        [SerializeField] private Sprite sprite;

        public string Id => id;
        public Sprite Sprite => sprite;

        public DeckSkinBack()
        {
        }

        public DeckSkinBack(string backId, Sprite backSprite)
        {
            id = backId;
            sprite = backSprite;
        }
    }

    [CreateAssetMenu(menuName = "Mejnun/Deck Skin")]
    public sealed class DeckSkin : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string artist;
        [SerializeField] private Sprite[] faces = new Sprite[Card.CardsPerDeck];
        [SerializeField] private Sprite cardBack;
        [SerializeField] private List<DeckSkinBack> alternateBacks = new List<DeckSkinBack>();
        [SerializeField] private Sprite blank;
        [SerializeField] private bool pixelArt = true;
        [SerializeField] private bool fourColourInk = true;
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private UnlockRequirement unlock = new UnlockRequirement();

        public string Id => id;
        public string DisplayName => ResolveDisplayName(id, displayName);
        // The English name stored in the asset; editor tools copy this, never
        // the localized DisplayName.
        public string SerializedDisplayName => displayName;
        public string Artist => artist;
        public string ArtistCredit => FormatArtistCredit(artist);
        public Sprite CardBack => cardBack;
        public IReadOnlyList<DeckSkinBack> AlternateBacks => alternateBacks;
        public string[] BackIds
        {
            get
            {
                if (alternateBacks == null)
                {
                    return Array.Empty<string>();
                }

                var ids = new string[alternateBacks.Count];
                for (var index = 0; index < alternateBacks.Count; index++)
                {
                    ids[index] = alternateBacks[index] == null ? null : alternateBacks[index].Id;
                }

                return ids;
            }
        }

        public Sprite Blank => blank;
        public bool PixelArt => pixelArt;
        public bool FourColourInk => fourColourInk;
        public float PixelsPerUnit => pixelsPerUnit;
        public UnlockRequirement Unlock => unlock ?? UnlockRequirement.Free;

        public Sprite GetFace(Suit suit, Rank rank)
        {
            var suitIndex = (int)suit;
            var rankIndex = (int)rank;
            if (suitIndex < 0 || suitIndex >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(suit), suit, "Suit must be a standard card suit.");
            }

            if (rankIndex < 0 || rankIndex >= 13)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be a standard card rank.");
            }

            if (faces == null || faces.Length != Card.CardsPerDeck)
            {
                throw new InvalidOperationException("A deck skin must contain exactly 52 face sprites.");
            }

            return faces[suitIndex * 13 + rankIndex];
        }

        public Sprite GetBack(string backId)
        {
            if (!string.IsNullOrEmpty(backId) && alternateBacks != null)
            {
                for (var index = 0; index < alternateBacks.Count; index++)
                {
                    var alternate = alternateBacks[index];
                    if (alternate != null && string.Equals(alternate.Id, backId, StringComparison.Ordinal))
                    {
                        return alternate.Sprite == null ? cardBack : alternate.Sprite;
                    }
                }
            }

            return cardBack;
        }

        public void Configure(string skinId, string skinDisplayName, Sprite[] faceSprites, Sprite backSprite,
            Sprite blankSprite, bool usePixelArt, float ppu, string skinArtist = null)
        {
            Configure(skinId, skinDisplayName, faceSprites, backSprite, null, blankSprite, usePixelArt, ppu,
                skinArtist);
        }

        public void Configure(string skinId, string skinDisplayName, Sprite[] faceSprites, Sprite backSprite,
            IReadOnlyList<DeckSkinBack> alternateBackSprites, Sprite blankSprite, bool usePixelArt, float ppu,
            string skinArtist = null)
        {
            if (string.IsNullOrWhiteSpace(skinId) || skinId.IndexOf(':') >= 0)
            {
                throw new ArgumentException("A deck skin must have an id without a colon.", nameof(skinId));
            }

            if (faceSprites == null || faceSprites.Length != Card.CardsPerDeck)
            {
                throw new ArgumentException("A deck skin must have exactly 52 face sprites.", nameof(faceSprites));
            }

            for (var index = 0; index < faceSprites.Length; index++)
            {
                if (faceSprites[index] == null)
                {
                    throw new ArgumentException("A deck skin cannot contain a missing face sprite.", nameof(faceSprites));
                }
            }

            if (backSprite == null)
            {
                throw new ArgumentNullException(nameof(backSprite));
            }

            if (ppu <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ppu), ppu, "Pixels per unit must be positive.");
            }

            var configuredBacks = new List<DeckSkinBack>();
            var backIds = new HashSet<string>(StringComparer.Ordinal);
            if (alternateBackSprites != null)
            {
                for (var index = 0; index < alternateBackSprites.Count; index++)
                {
                    var alternate = alternateBackSprites[index];
                    if (alternate == null || string.IsNullOrWhiteSpace(alternate.Id) ||
                        alternate.Id.IndexOf(':') >= 0 || alternate.Sprite == null || !backIds.Add(alternate.Id))
                    {
                        throw new ArgumentException("Alternate backs must have unique ids and valid sprites.",
                            nameof(alternateBackSprites));
                    }

                    configuredBacks.Add(new DeckSkinBack(alternate.Id, alternate.Sprite));
                }
            }

            id = skinId;
            displayName = skinDisplayName ?? skinId;
            artist = skinArtist ?? string.Empty;
            faces = (Sprite[])faceSprites.Clone();
            cardBack = backSprite;
            alternateBacks = configuredBacks;
            blank = blankSprite;
            pixelArt = usePixelArt;
            pixelsPerUnit = ppu;
        }

        public void ConfigureArtist(string skinArtist)
        {
            artist = skinArtist ?? string.Empty;
        }

        public void ConfigureUnlock(UnlockRequirement requirement)
        {
            unlock = requirement ?? UnlockRequirement.Free;
        }

        // The deck's name lives in the UI String Table under a key derived from
        // its stable id. Skins without an entry (tests, new art) keep their
        // serialized name.
        public static string NameKey(string skinId)
        {
            return string.IsNullOrWhiteSpace(skinId)
                ? string.Empty
                : "cosmetic.deck." + skinId.Trim().ToLowerInvariant() + ".name";
        }

        public static string ResolveDisplayName(string skinId, string fallback)
        {
            var key = NameKey(skinId);
            return key.Length > 0 && Loc.Has(key) ? Loc.Get(key) : fallback;
        }

        // The artist's name is a proper noun and is shown as written.
        public static string FormatArtistCredit(string artistName)
        {
            return string.IsNullOrWhiteSpace(artistName)
                ? string.Empty
                : Loc.Format("cosmetic.deck.artist_credit", artistName);
        }
    }
}