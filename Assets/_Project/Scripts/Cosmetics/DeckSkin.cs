using System;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [CreateAssetMenu(menuName = "Mejnun/Deck Skin")]
    public sealed class DeckSkin : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite[] faces = new Sprite[Card.CardsPerDeck];
        [SerializeField] private Sprite cardBack;
        [SerializeField] private Sprite blank;
        [SerializeField] private bool pixelArt = true;
        [SerializeField] private float pixelsPerUnit = 100f;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite CardBack => cardBack;
        public Sprite Blank => blank;
        public bool PixelArt => pixelArt;
        public float PixelsPerUnit => pixelsPerUnit;

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

        public void Configure(string skinId, string skinDisplayName, Sprite[] faceSprites, Sprite backSprite,
            Sprite blankSprite, bool usePixelArt, float ppu)
        {
            if (string.IsNullOrWhiteSpace(skinId))
            {
                throw new ArgumentException("A deck skin must have an id.", nameof(skinId));
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

            if (blankSprite == null)
            {
                throw new ArgumentNullException(nameof(blankSprite));
            }

            if (ppu <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(ppu), ppu, "Pixels per unit must be positive.");
            }

            id = skinId;
            displayName = skinDisplayName ?? skinId;
            faces = (Sprite[])faceSprites.Clone();
            cardBack = backSprite;
            blank = blankSprite;
            pixelArt = usePixelArt;
            pixelsPerUnit = ppu;
        }
    }
}