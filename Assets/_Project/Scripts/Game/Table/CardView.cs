using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public enum CardInkMode
    {
        None,
        RedToBlue,
        BlackToGreen
    }

    public static class CardInkSettings
    {
        public static CardInkMode ModeForSuit(Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs:
                    return CardInkMode.BlackToGreen;
                case Suit.Diamonds:
                    return CardInkMode.RedToBlue;
                default:
                    return CardInkMode.None;
            }
        }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CardView : MonoBehaviour
    {
        public const int DragSortingOrder = 1000;

        [SerializeField] private float tweenDuration = 0.18f;

        private SpriteRenderer spriteRenderer;
        private Material originalMaterial;
        private static Material fourColourMaterial;
        private static readonly int InkModeId = Shader.PropertyToID("_InkMode");
        private MaterialPropertyBlock inkProperties;
        private Sprite faceSprite;
        private Sprite backSprite;
        private Suit suit;
        private Vector3 tweenStart;
        private Vector3 tweenTarget;
        private Vector3 tweenScaleStart;
        private Vector3 tweenScaleTarget;
        private float tweenElapsed;
        private float pixelsPerUnit = 100f;
        private float currentTweenDuration;
        private float nextMoveDuration = -1f;
        private bool moving;
        private bool handLayer;
        private bool faceUp;
        private bool selected;
        private bool hovered;
        private bool hinted;
        private bool lastCardWarning;
        private bool tutorialFocus;
        private bool dimmed;
        private bool supportsFourColourInk = true;
        private SpriteRenderer cardHighlight;
        private SpriteRenderer focusGlow;
        private SpriteRenderer hintGlow;
        private SpriteRenderer hintGlowBloom;
        private SpriteRenderer shadowRenderer;
        private TextMesh warningText;
        private Quaternion layoutRotation = Quaternion.identity;
        private float layoutScale = 1f;
        private float baseScale = 1f;
        private Vector3 liftBasePosition;
        private Vector3 lastWrittenPosition;
        private float liftAmount;
        private bool hasWrittenPosition;
        private int sortingOrder;

        public byte CardId { get; private set; }
        public byte Deck { get; private set; }
        public bool IsFaceUp => faceUp;
        public bool IsSelected => selected;
        public bool IsHovered => hovered;
        public bool IsHinted => hinted;
        public bool IsTutorialFocused => tutorialFocus;
        public bool IsDimmed => dimmed;
        public bool IsMoving => moving;
        public Vector3 TargetLocalPosition => moving ? tweenTarget : GetBaseLocalPosition();
        public Sprite CurrentFaceSprite => faceSprite;
        public SpriteRenderer Renderer => spriteRenderer;
        public float BaseScale => baseScale;
        public float LayoutScale => layoutScale;
        public int SortingOrder => sortingOrder;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalMaterial = spriteRenderer.sharedMaterial;
        }

        private void OnEnable()
        {
            CardShadowSettings.Changed += ApplyShadowSettings;
            GameSettings.Changed += OnSettingsChanged;
            ApplyShadowSettings();
            ApplyFourColourInk();
        }

        private void OnDisable()
        {
            CardShadowSettings.Changed -= ApplyShadowSettings;
            GameSettings.Changed -= OnSettingsChanged;
        }

        public void ConfigureCamera(TableCamera camera, float ppu)
        {
            tableCamera = camera;
            pixelsPerUnit = ppu > 0f ? ppu : 100f;
        }

        public void SetCard(Card card, Sprite face, Sprite back, bool showFace, bool isSelected,
            bool useFourColourInk = true)
        {
            CardId = card.Id;
            Deck = card.Deck;
            suit = card.Suit;
            faceSprite = face;
            backSprite = back;
            supportsFourColourInk = useFourColourInk;
            faceUp = showFace;
            SetSelected(isSelected);
            UpdateSprite();
            SetLastCardWarning(false);
        }

        public void SetFaceUp(bool showFace)
        {
            if (faceUp == showFace)
            {
                return;
            }

            faceUp = showFace;
            UpdateSprite();
        }

        public void SetSelected(bool isSelected)
        {
            selected = isSelected;
            ApplyTint();
            RefreshCardHighlight();
        }

        public void SetHovered(bool isHovered)
        {
            hovered = isHovered;
            RefreshCardHighlight();
        }

        /// <summary>Outline playable cards; selection tint remains independent.</summary>
        public void SetHinted(bool isHinted)
        {
            hinted = isHinted;
            ApplyTint();
            RefreshHintOutline();
        }

        /// <summary>The tutorial's "move this card" highlight: a pulsing gold outline, separate from hints.</summary>
        public void SetTutorialFocus(bool focused)
        {
            if (tutorialFocus == focused)
            {
                return;
            }

            tutorialFocus = focused;
            if (focused && focusGlow == null && spriteRenderer != null)
            {
                focusGlow = CreateHintSprite("Tutorial Focus Glow", 1.1f);
            }

            if (focusGlow != null)
            {
                focusGlow.enabled = focused;
                focusGlow.sprite = spriteRenderer.sprite;
                UpdateFocusGlow(Time.unscaledTime);
            }
        }

        /// <summary>Darkens cards the tutorial is not pointing at.</summary>
        public void SetDimmed(bool isDimmed)
        {
            if (dimmed == isDimmed)
            {
                return;
            }

            dimmed = isDimmed;
            ApplyTint();
        }

        public void SetLastCardWarning(bool show)
        {
            lastCardWarning = show;
            if (show)
            {
                EnsureWarningText();
            }

            if (warningText != null)
            {
                warningText.gameObject.SetActive(show);
            }
        }

        public void Shake()
        {
            if (!GameSettings.ReducedMotion)
            {
                transform.localRotation = layoutRotation * Quaternion.Euler(0f, 0f, 9f);
            }
        }

        public void SetLayoutPose(Quaternion rotation, float scale)
        {
            layoutRotation = rotation;
            layoutScale = Mathf.Max(0.01f, scale);
            transform.localRotation = layoutRotation;
        }

        public void ApplyScale(float scale)
        {
            layoutScale = Mathf.Max(0.01f, scale);
            moving = false;
            transform.localScale = Vector3.one * (baseScale * layoutScale);
        }

        public void SetStartScale(float relativeScale)
        {
            moving = false;
            transform.localScale = Vector3.one * (baseScale * layoutScale * Mathf.Max(0.01f, relativeScale));
        }

        public void SetSortingOrder(int sortingOrder)
        {
            this.sortingOrder = sortingOrder;
            var rendererOrder = sortingOrder * 4;
            if (hintGlow != null)
            {
                hintGlow.sortingOrder = rendererOrder + 1;
            }

            if (focusGlow != null)
            {
                focusGlow.sortingOrder = rendererOrder + 1;
            }

            if (cardHighlight != null)
            {
                cardHighlight.sortingOrder = rendererOrder + 1;
            }

            if (hintGlowBloom != null)
            {
                hintGlowBloom.sortingOrder = rendererOrder + 1;
            }

            if (warningText != null)
            {
                warningText.GetComponent<MeshRenderer>().sortingOrder = rendererOrder + 3;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = rendererOrder + 2;
            }

            if (shadowRenderer != null)
            {
                shadowRenderer.sortingOrder = rendererOrder;
            }
        }

        public void SetParentKeepWorld(Transform parent, bool isHandLayer)
        {
            if (transform.parent != parent)
            {
                transform.SetParent(parent, true);
            }

            handLayer = isHandLayer;
        }

        public void MoveTo(Vector3 position, float duration = -1f)
        {
            MoveTo(position, duration, 1f);
        }

        public void MoveTo(Vector3 position, float duration, float targetScaleMultiplier)
        {
            var basePosition = GetBaseLocalPosition();
            tweenTarget = position;
            var targetScale = Vector3.one * (baseScale * layoutScale * Mathf.Max(0.01f, targetScaleMultiplier));
            tweenScaleTarget = targetScale;
            var moveDuration = duration;
            if (moveDuration < 0f && nextMoveDuration >= 0f)
            {
                moveDuration = nextMoveDuration;
            }
            else if (moveDuration < 0f)
            {
                moveDuration = tweenDuration;
            }

            nextMoveDuration = -1f;
            if (moveDuration <= 0f || (Vector3.SqrMagnitude(basePosition - position) < 0.000001f &&
                                       Vector3.SqrMagnitude(transform.localScale - targetScale) < 0.000001f))
            {
                moving = false;
                liftBasePosition = Snap(position);
                transform.localScale = targetScale;
                WriteLiftedPosition();
                return;
            }

            tweenStart = basePosition;
            tweenScaleStart = transform.localScale;
            tweenElapsed = 0f;
            currentTweenDuration = moveDuration;
            moving = true;
        }

        public void SetNextMoveDuration(float duration)
        {
            nextMoveDuration = Mathf.Max(0f, duration);
        }

        private void Update()
        {
            GetBaseLocalPosition();
            var time = Time.unscaledTime;
            var animationRotation = Quaternion.identity;
            if (hinted && GameSettings.HintJiggle && !GameSettings.ReducedMotion)
            {
                animationRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 10f) * 2.2f);
            }

            if (lastCardWarning && warningText != null)
            {
                var pulse = 1f + Mathf.Sin(time * 6f) * 0.12f;
                warningText.transform.localScale = Vector3.one * pulse;
                if (!GameSettings.ReducedMotion)
                {
                    animationRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 12f) * 4f);
                }
            }

            transform.localRotation = layoutRotation * animationRotation;
            UpdateHintGlow(time);
            UpdateFocusGlow(time);
            UpdateCardHighlight();

            if (moving)
            {
                FrameRateGovernor.NotifyActivity();
                tweenElapsed += Time.unscaledDeltaTime;
                var amount = Mathf.Clamp01(tweenElapsed / currentTweenDuration);
                liftBasePosition = Snap(Vector3.Lerp(tweenStart, tweenTarget, amount));
                transform.localScale = Vector3.Lerp(tweenScaleStart, tweenScaleTarget, amount);
                if (amount >= 1f)
                {
                    moving = false;
                }
            }

            var liftTarget = selected ? 0.14f : hovered && GameSettings.CardHoverHighlight ? 0.10f : 0f;
            var suppressLift = sortingOrder >= DragSortingOrder - 1;
            if (suppressLift)
            {
                liftTarget = 0f;
            }

            liftAmount = suppressLift || GameSettings.ReducedMotion
                ? liftTarget
                : Mathf.MoveTowards(liftAmount, liftTarget, Time.unscaledDeltaTime * 8f);
            WriteLiftedPosition();
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            var bounds = spriteRenderer.sprite == null
                ? new Bounds(Vector3.zero, new Vector3(0.64f, 0.97f, 0f))
                : spriteRenderer.sprite.bounds;
            var localPoint = transform.InverseTransformPoint(worldPoint + GetWorldLiftOffset());
            return localPoint.x >= bounds.min.x && localPoint.x <= bounds.max.x &&
                   localPoint.y >= bounds.min.y && localPoint.y <= bounds.max.y;
        }

        public bool RestBoundsContains(Vector3 worldPoint)
        {
            return ContainsWorldPoint(worldPoint);
        }

        private TableCamera tableCamera;

        private void UpdateSprite()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = faceUp ? faceSprite : backSprite;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
            baseScale = spriteRenderer.sprite == null || spriteRenderer.sprite.bounds.size.y <= 0f
                ? 1f
                : 0.92f / spriteRenderer.sprite.bounds.size.y;
            if (!moving)
            {
                transform.localScale = Vector3.one * (baseScale * layoutScale);
            }

            RefreshShadow();
            ApplyFourColourInk();
            ApplyTint();
            RefreshCardHighlight();
            RefreshHintGlow();
        }

        private void OnDestroy()
        {
            if (spriteRenderer != null)
                spriteRenderer.sharedMaterial = originalMaterial;
        }

        private void OnSettingsChanged(string key)
        {
            if (key == "settings.accessibility.fourColourDeck")
                ApplyFourColourInk();
        }

        private void ApplyFourColourInk()
        {
            if (spriteRenderer == null)
                return;

            if (originalMaterial == null)
                originalMaterial = spriteRenderer.sharedMaterial;

            var mode = faceUp && supportsFourColourInk && GameSettings.FourColourDeck
                ? CardInkSettings.ModeForSuit(suit)
                : CardInkMode.None;
            if (mode == CardInkMode.None)
            {
                spriteRenderer.sharedMaterial = originalMaterial;
                spriteRenderer.SetPropertyBlock(null);
                return;
            }

            if (fourColourMaterial == null)
            {
                var shader = Resources.Load<Shader>("CardInk");
                if (shader == null)
                    return;
                fourColourMaterial = new Material(shader);
            }

            spriteRenderer.sharedMaterial = fourColourMaterial;
            inkProperties ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(inkProperties);
            inkProperties.SetFloat(InkModeId, (float)mode);
            spriteRenderer.SetPropertyBlock(inkProperties);
        }

        private void RefreshShadow()
        {
            ApplyShadowSettings();
        }

        private void ApplyShadowSettings()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.sprite != null && shadowRenderer == null)
            {
                var shadowObject = new GameObject("Card Shadow");
                shadowObject.transform.SetParent(transform, false);
                shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
                shadowRenderer.sharedMaterial = GetHintMaterial();
                shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            }

            if (shadowRenderer != null)
            {
                var settings = CardShadowSettings.Active;
                shadowRenderer.sprite = spriteRenderer.sprite;
                shadowRenderer.transform.localPosition = new Vector3(settings.Offset.x, settings.Offset.y, 0.01f);
                shadowRenderer.transform.localScale = Vector3.one * settings.Scale;
                shadowRenderer.color = settings.Color;
                shadowRenderer.enabled = settings.IsEnabled && spriteRenderer.sprite != null;
            }
        }

        private void ApplyTint()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (dimmed)
            {
                spriteRenderer.color = new Color(0.5f, 0.5f, 0.55f, 1f);
            }
            else
            {
                spriteRenderer.color = Color.white;
            }
        }

        private void RefreshCardHighlight()
        {
            if ((selected || hovered && GameSettings.CardHoverHighlight) && cardHighlight == null &&
                spriteRenderer != null && spriteRenderer.sprite != null)
            {
                cardHighlight = CreateHintSprite("Card Highlight", 1.08f);
            }

            UpdateCardHighlight();
        }

        private void UpdateCardHighlight()
        {
            if (cardHighlight == null)
            {
                return;
            }

            var visible = selected || hovered && GameSettings.CardHoverHighlight;
            cardHighlight.enabled = visible;
            cardHighlight.color = new Color(1f, 0.78f, 0.12f, selected ? 0.78f : 0.48f);
            cardHighlight.sprite = spriteRenderer.sprite;
        }

        private void RefreshHintOutline()
        {
            if (hinted && hintGlow == null)
            {
                hintGlowBloom = CreateHintSprite("Hint Glow Bloom", 1.14f);
                hintGlow = CreateHintSprite("Hint Glow", 1.06f);
            }

            if (hintGlow != null)
            {
                hintGlow.enabled = hinted;
                hintGlowBloom.enabled = hinted;
                RefreshHintGlow();
            }
        }

        private SpriteRenderer CreateHintSprite(string objectName, float scale)
        {
            var glowObject = new GameObject(objectName);
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localScale = Vector3.one * scale;
            glowObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            var renderer = glowObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = GetHintMaterial();
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder * 4 + 1;
            renderer.sprite = spriteRenderer.sprite;
            renderer.enabled = hinted;
            return renderer;
        }

        private static Material GetHintMaterial()
        {
            if (hintMaterial != null)
            {
                return hintMaterial;
            }

            var shader = Resources.Load<Shader>("Shaders/MejnunSpriteSilhouette");
            if (shader == null)
            {
                shader = Shader.Find("Mejnun/Sprite Silhouette") ?? Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                hintMaterial = new Material(shader);
            }

            return hintMaterial;
        }

        private void RefreshHintGlow()
        {
            if (hintGlow != null)
            {
                hintGlow.sprite = spriteRenderer.sprite;
            }

            if (hintGlowBloom != null)
            {
                hintGlowBloom.sprite = spriteRenderer.sprite;
            }

            if (focusGlow != null)
            {
                focusGlow.sprite = spriteRenderer.sprite;
            }

            if (cardHighlight != null)
            {
                cardHighlight.sprite = spriteRenderer.sprite;
            }
        }

        private Vector3 GetBaseLocalPosition()
        {
            if (!hasWrittenPosition || (transform.localPosition - lastWrittenPosition).sqrMagnitude > 0.000001f)
            {
                liftBasePosition = transform.localPosition;
            }

            return liftBasePosition;
        }

        private void WriteLiftedPosition()
        {
            transform.localPosition = liftBasePosition + GetLocalLiftOffset();
            lastWrittenPosition = transform.localPosition;
            hasWrittenPosition = true;
        }

        private Vector3 GetLocalLiftOffset()
        {
            return layoutRotation * Vector3.up * (0.92f * layoutScale * liftAmount);
        }

        private Vector3 GetWorldLiftOffset()
        {
            var parent = transform.parent;
            return parent == null ? GetLocalLiftOffset() : parent.TransformVector(GetLocalLiftOffset());
        }

        private void UpdateFocusGlow(float time)
        {
            if (!tutorialFocus || focusGlow == null)
            {
                return;
            }

            var pulse = GameSettings.ReducedMotion ? 0.5f : (Mathf.Sin(time * 5f) + 1f) * 0.5f;
            focusGlow.color = new Color(1f, 0.84f, 0.2f, 0.45f + pulse * 0.35f);
            focusGlow.transform.localScale = Vector3.one * (1.08f + pulse * 0.04f);
        }

        private void UpdateHintGlow(float time)
        {
            if (!hinted || hintGlow == null || hintGlowBloom == null)
            {
                return;
            }

            var pulse = GameSettings.ReducedMotion ? 0f : (Mathf.Sin(time * 4.5f) + 1f) * 0.5f;
            hintGlow.color = new Color(0.56f, 1f, 0.42f, 0.34f + pulse * 0.16f);
            hintGlowBloom.color = new Color(0.48f, 1f, 0.36f, 0.12f + pulse * 0.1f);
            hintGlow.transform.localScale = Vector3.one * (1.06f + pulse * 0.025f);
            hintGlowBloom.transform.localScale = Vector3.one * (1.14f + pulse * 0.04f);
        }

        private void EnsureWarningText()
        {
            if (warningText != null)
            {
                return;
            }

            var warningObject = new GameObject("Last Card Warning");
            warningObject.transform.SetParent(transform, false);
            warningObject.transform.localPosition = new Vector3(0.24f, 0.38f, -0.02f);
            warningText = warningObject.AddComponent<TextMesh>();
            warningText.text = "!";
            warningText.anchor = TextAnchor.MiddleCenter;
            warningText.alignment = TextAlignment.Center;
            warningText.fontSize = 64;
            warningText.characterSize = 0.12f;
            warningText.color = new Color(1f, 0.12f, 0.08f, 1f);
            warningText.GetComponent<MeshRenderer>().sortingOrder = sortingOrder * 4 + 3;
        }

        private Vector3 Snap(Vector3 position)
        {
            if (handLayer || tableCamera == null)
            {
                return position;
            }

            var parent = transform.parent;
            var world = parent == null ? position : parent.TransformPoint(position);
            world = tableCamera.SnapWorldPosition(world, pixelsPerUnit);
            return parent == null ? world : parent.InverseTransformPoint(world);
        }

        private static Material hintMaterial;
    }
}