using System;
using Mejnun.Core;
using Mejnun.Localization;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    public enum FeltRenderMode
    {
        Tiled,
        TiledWithBorder,
        Image
    }

    [CreateAssetMenu(menuName = "Mejnun/Table Felt")]
    public sealed class TableFelt : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private FeltRenderMode mode;
        [SerializeField] private Sprite tile;
        [SerializeField] private float tileWorldSize = 1f;
        [SerializeField] private Sprite border;
        [SerializeField] private Sprite image;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Color clearColor = Color.green;
        [SerializeField] private UnlockRequirement unlock = new UnlockRequirement();
        [SerializeField] private FeltBorderSettings borderSettings = new FeltBorderSettings();

        public string Id => id;
        public string DisplayName => ResolveDisplayName(id, displayName);
        // The English name stored in the asset; editor tools copy this, never
        // the localized DisplayName.
        public string SerializedDisplayName => displayName;
        public FeltRenderMode Mode => mode;
        public Sprite Tile => tile;
        public float TileWorldSize => tileWorldSize;
        public Sprite Border => border;
        public Sprite Image => image;
        public Color Tint => tint;
        public Color ClearColor => clearColor;
        public UnlockRequirement Unlock => unlock ?? UnlockRequirement.Free;
        public FeltBorderSettings BorderSettings => borderSettings ?? (borderSettings = new FeltBorderSettings());

        public void Configure(string feltId, string feltDisplayName, FeltRenderMode renderMode, Sprite tileSprite,
            float worldTileSize, Sprite borderSprite, Sprite imageSprite, Color feltTint, Color feltClearColor,
            UnlockRequirement requirement)
        {
            if (string.IsNullOrWhiteSpace(feltId))
            {
                throw new ArgumentException("A felt must have an id.", nameof(feltId));
            }

            if (!Enum.IsDefined(typeof(FeltRenderMode), renderMode))
            {
                throw new ArgumentOutOfRangeException(nameof(renderMode), renderMode, "Unknown felt render mode.");
            }

            if (renderMode != FeltRenderMode.Image && tileSprite == null)
            {
                throw new ArgumentNullException(nameof(tileSprite));
            }

            if (renderMode == FeltRenderMode.TiledWithBorder && borderSprite == null)
            {
                throw new ArgumentNullException(nameof(borderSprite));
            }

            if (renderMode == FeltRenderMode.Image && imageSprite == null)
            {
                throw new ArgumentNullException(nameof(imageSprite));
            }

            if (worldTileSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(worldTileSize), worldTileSize,
                    "Felt tile size must be positive.");
            }

            id = feltId;
            displayName = string.IsNullOrWhiteSpace(feltDisplayName) ? feltId : feltDisplayName;
            mode = renderMode;
            tile = tileSprite;
            tileWorldSize = worldTileSize;
            border = borderSprite;
            image = imageSprite;
            tint = feltTint;
            clearColor = feltClearColor;
            unlock = requirement ?? UnlockRequirement.Free;
        }

        public void ConfigureBorder(bool enabled, float thickness, float referenceHeight, Color borderTint,
            float inset)
        {
            BorderSettings.Configure(enabled, thickness, referenceHeight, borderTint, inset);
        }

        // The felt's name lives in the UI String Table under a key derived from
        // its stable id. Felts without an entry keep their serialized name.
        public static string NameKey(string feltId)
        {
            return string.IsNullOrWhiteSpace(feltId)
                ? string.Empty
                : "cosmetic.felt." + feltId.Trim().ToLowerInvariant() + ".name";
        }

        public static string ResolveDisplayName(string feltId, string fallback)
        {
            var key = NameKey(feltId);
            return key.Length > 0 && Loc.Has(key) ? Loc.Get(key) : fallback;
        }
    }
}