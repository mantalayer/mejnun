using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CardView : MonoBehaviour
    {
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
        private bool moving;
        private bool handLayer;
        private bool faceUp;
        private bool selected;
        private bool hinted;
        private bool lastCardWarning;
        private LineRenderer hintOutline;
        private TextMesh warningText;
        private Quaternion layoutRotation = Quaternion.identity;
        private float layoutScale = 1f;
        private int sortingOrder;

        public byte CardId { get; private set; }
        public byte Deck { get; private set; }
        public bool IsFaceUp => faceUp;
        public bool IsSelected => selected;
        public bool IsHinted => hinted;
        public Sprite CurrentFaceSprite => faceSprite;
        public SpriteRenderer Renderer => spriteRenderer;
        public float LayoutScale => layoutScale;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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

        public void SetSortingOrder(int sortingOrder)
        {
            this.sortingOrder = sortingOrder;
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = sortingOrder;
            }

            if (hintOutline != null)
            {
                hintOutline.sortingOrder = sortingOrder + 1;
            }

            if (warningText != null)
            {
                warningText.GetComponent<MeshRenderer>().sortingOrder = sortingOrder + 2;
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
            var targetScale = Vector3.one * layoutScale;
            var moveDuration = duration < 0f ? tweenDuration : duration;
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

            if (moving)
            {
                tweenElapsed += Time.deltaTime;
                var amount = Mathf.Clamp01(tweenElapsed / currentTweenDuration);
                transform.localPosition = Snap(Vector3.Lerp(tweenStart, tweenTarget, amount));
                transform.localScale = Vector3.Lerp(tweenScaleStart, Vector3.one * layoutScale, amount);
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
            ApplyTint();
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
            if (hinted && hintOutline == null)
            {
                var outlineObject = new GameObject("Hint Outline");
                outlineObject.transform.SetParent(transform, false);
                hintOutline = outlineObject.AddComponent<LineRenderer>();
                hintOutline.useWorldSpace = false;
                hintOutline.loop = true;
                hintOutline.positionCount = 4;
                const float outlineWidth = 0.02f;
                hintOutline.widthMultiplier = outlineWidth;
                hintOutline.startColor = new Color(0.62f, 1f, 0.49f, 1f);
                hintOutline.endColor = hintOutline.startColor;
                hintOutline.sortingOrder = sortingOrder + 1;
                hintOutline.material = new Material(Shader.Find("Sprites/Default"));
                var bounds = faceSprite == null ? new Bounds(Vector3.zero, new Vector3(0.64f, 0.97f, 0f)) : faceSprite.bounds;
                var center = bounds.center;
                var spritePixelsPerUnit = faceSprite == null ? pixelsPerUnit : faceSprite.pixelsPerUnit;
                var padding = new Vector3(2f, 2.5f, 0f) / spritePixelsPerUnit;
                var half = bounds.extents - padding + new Vector3(outlineWidth * 0.5f, outlineWidth * 0.5f, 0f);
                outlineObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                hintOutline.SetPosition(0, center + new Vector3(-half.x, -half.y, 0f));
                hintOutline.SetPosition(1, center + new Vector3(-half.x, half.y, 0f));
                hintOutline.SetPosition(2, center + new Vector3(half.x, half.y, 0f));
                hintOutline.SetPosition(3, center + new Vector3(half.x, -half.y, 0f));
            }

            if (hintOutline != null)
            {
                hintOutline.enabled = hinted;
            }
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
            warningText.GetComponent<MeshRenderer>().sortingOrder = sortingOrder + 2;
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
    }
}