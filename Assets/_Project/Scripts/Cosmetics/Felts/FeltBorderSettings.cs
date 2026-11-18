using System;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [Serializable]
    public sealed class FeltBorderSettings
    {
        [SerializeField] private bool borderEnabled = true;
        [SerializeField, Min(0.1f)] private float thicknessPixels = 16f;
        [SerializeField, Min(1f)] private float referenceScreenHeight = 1080f;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0f)] private float insetPixels;

        public bool Enabled => borderEnabled;
        public float ThicknessPixels => thicknessPixels;
        public float ReferenceScreenHeight => referenceScreenHeight;
        public Color Tint => tint;
        public float InsetPixels => insetPixels;

        public void Configure(bool enabled, float thickness, float referenceHeight, Color borderTint,
            float inset)
        {
            if (thickness <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(thickness));
            }

            if (referenceHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(referenceHeight));
            }

            if (inset < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(inset));
            }

            borderEnabled = enabled;
            thicknessPixels = thickness;
            referenceScreenHeight = referenceHeight;
            tint = borderTint;
            insetPixels = inset;
        }
    }
}