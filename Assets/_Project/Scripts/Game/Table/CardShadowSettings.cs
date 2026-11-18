using System;
using UnityEngine;

namespace Mejnun.Game
{
    [CreateAssetMenu(menuName = "Mejnun/Card Shadow Settings")]
    public sealed class CardShadowSettings : ScriptableObject
    {
        private const string ResourceName = "CardShadowSettings";

        [SerializeField] private bool shadowEnabled = true;
        [SerializeField] private Vector2 offset = new Vector2(0.035f, -0.05f);
        [SerializeField, Min(0.01f)] private float scale = 1.03f;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);

        private static CardShadowSettings active;

        public static event Action Changed;

        public static CardShadowSettings Active
        {
            get
            {
                if (active == null)
                {
                    active = Resources.Load<CardShadowSettings>(ResourceName);
                    if (active == null)
                    {
                        active = CreateInstance<CardShadowSettings>();
                    }
                }

                return active;
            }
        }

        public bool IsEnabled
        {
            get => shadowEnabled;
            set
            {
                if (shadowEnabled == value)
                {
                    return;
                }

                shadowEnabled = value;
                Changed?.Invoke();
            }
        }

        public Vector2 Offset
        {
            get => offset;
            set
            {
                if (offset == value)
                {
                    return;
                }

                offset = value;
                Changed?.Invoke();
            }
        }

        public float Scale
        {
            get => scale;
            set
            {
                if (Mathf.Approximately(scale, value))
                {
                    return;
                }

                scale = value;
                Changed?.Invoke();
            }
        }

        public Color Color
        {
            get => shadowColor;
            set
            {
                if (shadowColor == value)
                {
                    return;
                }

                shadowColor = value;
                Changed?.Invoke();
            }
        }

        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}