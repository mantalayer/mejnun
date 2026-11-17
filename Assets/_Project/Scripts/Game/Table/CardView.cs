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
        private float tweenElapsed;
        private float pixelsPerUnit = 100f;
        private float currentTweenDuration;
        private bool moving;
        private bool faceUp;
        private bool selected;
        private bool hinted;

        public byte CardId { get; private set; }
        public byte Deck { get; private set; }
        public bool IsFaceUp => faceUp;
        public bool IsSelected => selected;
        public bool IsHinted => hinted;
        public Sprite CurrentFaceSprite => faceSprite;
        public SpriteRenderer Renderer => spriteRenderer;

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

        /// <summary>"Highlight playable" hint tint. Selection wins when a card is both.</summary>
        public void SetHinted(bool isHinted)
        {
            hinted = isHinted;
            ApplyTint();
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = sortingOrder;
            }
        }

        public void MoveTo(Vector3 position, float duration = -1f)
        {
            tweenTarget = position;
            var moveDuration = duration < 0f ? tweenDuration : duration;
            if (moveDuration <= 0f || Vector3.SqrMagnitude(transform.position - position) < 0.000001f)
            {
                moving = false;
                transform.position = Snap(position);
                return;
            }

            tweenStart = transform.position;
            tweenElapsed = 0f;
            currentTweenDuration = moveDuration;
            moving = true;
        }

        private void Update()
        {
            if (moving)
            {
                tweenElapsed += Time.deltaTime;
                var amount = Mathf.Clamp01(tweenElapsed / currentTweenDuration);
                transform.position = Snap(Vector3.Lerp(tweenStart, tweenTarget, amount));
                if (amount >= 1f)
                {
                    moving = false;
                }
            }
            else if (tableCamera != null)
            {
                transform.position = Snap(transform.position);
            }
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
            else if (hinted)
            {
                spriteRenderer.color = new Color(0.72f, 1f, 0.72f, 1f);
            }
            else
            {
                spriteRenderer.color = Color.white;
            }
        }

        private Vector3 Snap(Vector3 position)
        {
            return tableCamera == null ? position : tableCamera.SnapWorldPosition(position, pixelsPerUnit);
        }
    }
}