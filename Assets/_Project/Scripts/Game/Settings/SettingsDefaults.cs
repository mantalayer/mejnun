using UnityEngine;
using UnityEngine.Serialization;

namespace Mejnun.Game
{
    [CreateAssetMenu(fileName = "SettingsDefaults", menuName = "Mejnun/Settings Defaults")]
    public sealed class SettingsDefaults : ScriptableObject
    {
        public const string ResourcesPath = "SettingsDefaults";

        [Header("Gameplay")]
        public bool ConfirmRevert = true;
        public bool ConfirmDraw;
        public bool ConfirmFix = true;
        public bool ConfirmLeave;
        public bool HintJiggle = true;
        public bool CardHoverHighlight = true;
        public bool ReducedMotion;
        public bool HapticsEnabled = true;
        public bool EasyOpeningDeal;

        [Header("Accessibility")]
        public bool FourColourDeck;
        public bool LeftHanded;
        public bool BatterySaver;

        [Header("Hand")]
        public float HandArcAmount = 0.246f;
        public float HandSpread = 0.5147f;
        public float HandCardScale = 1.245f;
        public float HandLift = 0.149f;

        [Header("Table")]
        public float OpponentCardScale = 1f;
        public bool HideOpponentHandsMobile = true;
        public bool HideOpponentHandsDesktop;
        public float EdgePanWidthMobile = 0.14f;
        [FormerlySerializedAs("EdgePanWidth")] public float EdgePanWidthDesktop = 0.08f;
        public float EdgeScrollWidthMobile = 0.14f;
        public float EdgeScrollWidthDesktop = 0.08f;
        public float EdgePanSpeed = 1f;
        public float TouchPanStartSpeed = 150f;
        [FormerlySerializedAs("QuickPickupSeconds")] public float LongPressSeconds = 0.35f;
        public float ZoomSensitivity = 5f;
        public bool OverlapSets = true;
        public bool OrganicBoard = true;
        public float BoardCardSpread = 0.99f;
        public float CardMessiness = 0.251f;

        [Header("User Interface")]
        public float UiScaleMobile = 1.5f;
        public float UiScaleDesktop = 1f;

        [Header("CPU")]
        public int CpuSpeed = 3;
        public bool ShowFastForward = true;

        [Header("Audio")]
        [Range(0f, 1f)] public float VolumeMaster = 1f;
        [Range(0f, 1f)] public float VolumeMusic = 1f;
        [Range(0f, 1f)] public float VolumeSfx = 1f;
        [Range(0f, 1f)] public float VolumeVoice = 1f;

        [Header("Graphics")]
        public int DisplayMode;
        public bool VSync = true;
        public int FrameRateLimit = 1;

        [Header("Online")]
        public int QuickMatchBotFill = 30;

        private static SettingsDefaults active;

        public static SettingsDefaults Active
        {
            get
            {
                if (active == null)
                {
                    active = Resources.Load<SettingsDefaults>(ResourcesPath);
                    if (active == null)
                    {
                        active = CreateInstance<SettingsDefaults>();
                    }
                }

                return active;
            }
            set => active = value;
        }
    }
}