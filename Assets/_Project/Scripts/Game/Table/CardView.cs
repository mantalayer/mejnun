using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CardView : MonoBehaviour
    {
        public const int DragSortingOrder = 1000;

        [SerializeField] private float tweenDuration = 0.18f;

        private SpriteRenderer spriteRenderer;
        private Sprite faceSprite;
        private Sprite backSprite;
        private Vector3 tweenStart;
        private Vector3 tweenTarget;
        private Vector3 tweenScaleStart;
        private float tweenElapsed;
        private float pixelsPerUnit = 100f;
        private float currentTweenDuration;
        private float nextMoveDuration = -1f;
        private bool moving;
        private bool handLayer;
        private bool faceUp;
        private bool selected;
        private bool hinted;
        private bool lastCardWarning;
        private SpriteRenderer hintGlow;
        private SpriteRenderer hintGlowBloom;
        private SpriteRenderer shadowRenderer;
        private TextMesh warningText;
        private Quaternion layoutRotation = Quaternion.identity;
        private float layoutScale = 1f;
        private float baseScale = 1f;
        private int sortingOrder;

        public byte CardId { get; private set; }
        public byte Deck { get; private set; }
        public bool IsFaceUp => faceUp;
        public bool IsSelected => selected;
        public bool IsHinted => hinted;
        public Sprite CurrentFaceSprite => faceSprite;
        public SpriteRenderer Renderer => spriteRenderer;
        public float BaseScale => baseScale;
        public float LayoutScale => layoutScale;
        public int SortingOrder => sortingOrder;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            CardShadowSettings.Changed += ApplyShadowSettings;
            ApplyShadowSettings();
        }

        private void OnDisable()
        {
            CardShadowSettings.Changed -= ApplyShadowSettings;
        }

        public void ConfigureCamera(TableCamera camera, float ppu)
        {
            tableCamera = camera;
            pixelsPerUnit = ppu > 0f ? ppu : 100f;
        }

        public void SetCard(Card card, Sprite face, Sprite back, bool showFace, bool isSelected)
        {
            CardId = card.Id;
            Deck = card.Deck;
            faceSprite = face;
            backSprite = back;
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
        }

        /// <summary>Outline playable cards; selection tint remains independent.</summary>
        public void SetHinted(bool isHinted)
        {
            hinted = isHinted;
            ApplyTint();
            RefreshHintOutline();
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
            tweenTarget = position;
            var targetScale = Vector3.one * (baseScale * layoutScale);
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
            if (moveDuration <= 0f || (Vector3.SqrMagnitude(transform.localPosition - position) < 0.000001f &&
                                       Vector3.SqrMagnitude(transform.localScale - targetScale) < 0.000001f))
            {
                moving = false;
                transform.localPosition = Snap(position);
                transform.localScale = targetScale;
                return;
            }

            tweenStart = transform.localPosition;
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

            if (moving)
            {
                tweenElapsed += Time.deltaTime;
                var amount = Mathf.Clamp01(tweenElapsed / currentTweenDuration);
                transform.localPosition = Snap(Vector3.Lerp(tweenStart, tweenTarget, amount));
                transform.localScale = Vector3.Lerp(tweenScaleStart,
                    Vector3.one * (baseScale * layoutScale), amount);
                if (amount >= 1f)
                {
                    moving = false;
                }
            }
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
            var localPoint = transform.InverseTransformPoint(worldPoint);
            return localPoint.x >= bounds.min.x && localPoint.x <= bounds.max.x &&
                   localPoint.y >= bounds.min.y && localPoint.y <= bounds.max.y;
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
            ApplyTint();
            RefreshHintGlow();
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

            if (selected)
            {
                spriteRenderer.color = new Color(1f, 0.82f, 0.36f, 1f);
            }
            else
            {
                spriteRenderer.color = Color.white;
            }
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