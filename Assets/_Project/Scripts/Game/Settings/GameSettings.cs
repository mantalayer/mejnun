using UnityEngine;

namespace Mejnun.Game
{
    public static class GameSettings
    {
        private const string ConfirmRevertKey = "settings.confirm.revert";
        private const string ConfirmDrawKey = "settings.confirm.draw";
        private const string ConfirmFixKey = "settings.confirm.fix";
        private const string ConfirmLeaveKey = "settings.confirm.leave";
        private const string HintBoardMovesKey = "settings.hints.boardMoves";
        private const string HintJiggleKey = "settings.hints.jiggle";
        private const string CardHoverHighlightKey = "settings.cards.hoverHighlight";
        private const string ReducedMotionKey = "settings.reducedMotion";
        private const string HapticsKey = "settings.haptics";
        private const string FourColourDeckKey = "settings.accessibility.fourColourDeck";
        private const string LeftHandedKey = "settings.accessibility.leftHanded";
        private const string BatterySaverKey = "settings.accessibility.batterySaver";
        private const string HandArcAmountKey = "settings.hand.arcAmount";
        private const string HandSpreadKey = "settings.hand.spread";
        private const string HandCardScaleKey = "settings.hand.cardScale";
        private const string HandLiftKey = "settings.hand.lift";
        public const string OpponentCardScaleKey = "settings.opponents.cardScale";
        public const string HideOpponentHandsKey = "settings.opponents.hideIdle";
        public const string EdgePanWidthKey = "settings.camera.edgePanWidth";
        public const float DefaultEdgePanWidth = 0.08f;
        private const string ZoomSensitivityKey = "settings.camera.zoomSensitivity";
        private const string UiScaleKey = "settings.ui.scale";
        private const string CpuSpeedKey = "settings.cpu.speed.v2";
        private const string LegacyCpuSpeedKey = "settings.cpu.speed";
        private const string ShowFastForwardKey = "settings.cpu.showFastForward";
        private const string OverlapSetsKey = "settings.board.overlapSets";
        private const string OrganicBoardKey = "settings.board.organic";
        private const string BoardCardSpreadKey = "settings.board.cardSpread";
        private const string CardMessinessKey = "settings.board.cardMessiness";
        private const string EasyOpeningDealKey = "settings.game.easyOpeningDeal";
        private const string VolumeMasterKey = "settings.audio.master";
        private const string VolumeMusicKey = "settings.audio.music";
        private const string VolumeSfxKey = "settings.audio.sfx";
        private const string VolumeVoiceKey = "settings.audio.voice";
        private const string LanguageKey = Mejnun.Localization.LanguagePreference.PlayerPrefsKey;
        private const string QuickMatchBotFillKey = "settings.online.botFill";
        private const string OnlineNicknameKey = "settings.online.nickname";
        private const string TutorialCompletedKey = "settings.tutorial.completed";
        private static readonly float[] CpuDelayMultipliers = { 6f, 4f, 3f, 2f, 1f, 0.5f };
        private static bool fastForward;

        public static event System.Action<string> Changed;

        public static bool ConfirmRevert
        {
            get => Get(ConfirmRevertKey, true);
            set => Set(ConfirmRevertKey, value);
        }

        public static bool ConfirmDraw
        {
            get => Get(ConfirmDrawKey, false);
            set => Set(ConfirmDrawKey, value);
        }

        public static bool ConfirmFix
        {
            get => Get(ConfirmFixKey, true);
            set => Set(ConfirmFixKey, value);
        }

        public static bool ConfirmLeave
        {
            get => Get(ConfirmLeaveKey, false);
            set => Set(ConfirmLeaveKey, value);
        }

        public static bool HintBoardMoves
        {
            get => Get(HintBoardMovesKey, true);
            set => Set(HintBoardMovesKey, value);
        }

        public static bool HintJiggle
        {
            get => Get(HintJiggleKey, true);
            set => Set(HintJiggleKey, value);
        }

        public static bool CardHoverHighlight
        {
            get => Get(CardHoverHighlightKey, true);
            set => Set(CardHoverHighlightKey, value);
        }

        /// <summary>Set once the tutorial has been played (finished or skipped); stops the first-Play tutorial prompt.</summary>
        public static bool TutorialCompleted
        {
            get => Get(TutorialCompletedKey, false);
            set => Set(TutorialCompletedKey, value);
        }

        public static bool ReducedMotion
        {
            get => Get(ReducedMotionKey, false);
            set => Set(ReducedMotionKey, value);
        }

        public static bool HapticsEnabled
        {
            get => Get(HapticsKey, true);
            set => Set(HapticsKey, value);
        }

        public static bool FourColourDeck
        {
            get => Get(FourColourDeckKey, false);
            set => Set(FourColourDeckKey, value);
        }

        public static bool LeftHanded
        {
            get => Get(LeftHandedKey, false);
            set => Set(LeftHandedKey, value);
        }

        public static bool BatterySaver
        {
            get => Get(BatterySaverKey, false);
            set => Set(BatterySaverKey, value);
        }

        public static float HandArcAmount
        {
            get => Get(HandArcAmountKey, 0.246f);
            set => Set(HandArcAmountKey, value);
        }

        public static float HandSpread
        {
            get => Get(HandSpreadKey, 0.5147f);
            set => Set(HandSpreadKey, value);
        }

        public static float HandCardScale
        {
            get => Get(HandCardScaleKey, 1.245f);
            set => Set(HandCardScaleKey, value);
        }

        public static float HandLift
        {
            get => Get(HandLiftKey, 0.149f);
            set => Set(HandLiftKey, value);
        }

        public static float OpponentCardScale
        {
            get => Get(OpponentCardScaleKey, 1f);
            set => Set(OpponentCardScaleKey, value);
        }

        public static bool HideOpponentHands
        {
            get => Get(HideOpponentHandsKey, DeviceProfile.IsMobilePlatform);
            set => Set(HideOpponentHandsKey, value);
        }

        public static float EdgePanWidth
        {
            get => Mathf.Clamp(Get(EdgePanWidthKey, DefaultEdgePanWidth), 0f, 0.25f);
            set => Set(EdgePanWidthKey, Mathf.Clamp(value, 0f, 0.25f));
        }

        public static float ZoomSensitivity
        {
            get => Get(ZoomSensitivityKey, 5f);
            set => Set(ZoomSensitivityKey, value);
        }

        public static float UiScale
        {
            get => Mathf.Clamp(Get(UiScaleKey, DeviceProfile.IsMobilePlatform ? 1.5f : 1f), 0.8f, 3f);
            set => Set(UiScaleKey, Mathf.Clamp(value, 0.8f, 3f));
        }

        public static int CpuSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(CpuSpeedKey, 3), 0, 5);
            set
            {
                PlayerPrefs.SetInt(CpuSpeedKey, Mathf.Clamp(value, 0, 5));
                PlayerPrefs.Save();
                Changed?.Invoke(CpuSpeedKey);
            }
        }

        public static float CpuDelayMultiplier => CpuDelayMultipliers[CpuSpeed];

        public static bool ShowFastForward
        {
            get => Get(ShowFastForwardKey, true);
            set => Set(ShowFastForwardKey, value);
        }

        public static bool OverlapSets
        {
            get => Get(OverlapSetsKey, true);
            set => Set(OverlapSetsKey, value);
        }

        public static bool OrganicBoard
        {
            get => Get(OrganicBoardKey, true);
            set => Set(OrganicBoardKey, value);
        }

        public static float BoardCardSpread
        {
            get => Mathf.Clamp(Get(BoardCardSpreadKey, 0.99f), 0.5f, 1.5f);
            set => Set(BoardCardSpreadKey, Mathf.Clamp(value, 0.5f, 1.5f));
        }

        public static float CardMessiness
        {
            get => Mathf.Clamp01(Get(CardMessinessKey, 0.251f));
            set => Set(CardMessinessKey, Mathf.Clamp01(value));
        }

        public static bool EasyOpeningDeal
        {
            get => Get(EasyOpeningDealKey, false);
            set => Set(EasyOpeningDealKey, value);
        }

        public static bool FastForward
        {
            get => fastForward;
            set => fastForward = value;
        }

        public static float VolumeMaster
        {
            get => Mathf.Clamp01(Get(VolumeMasterKey, 1f));
            set => Set(VolumeMasterKey, Mathf.Clamp01(value));
        }

        public static float VolumeMusic
        {
            get => Mathf.Clamp01(Get(VolumeMusicKey, 1f));
            set => Set(VolumeMusicKey, Mathf.Clamp01(value));
        }

        public static float VolumeSfx
        {
            get => Mathf.Clamp01(Get(VolumeSfxKey, 1f));
            set => Set(VolumeSfxKey, Mathf.Clamp01(value));
        }

        public static float VolumeVoice
        {
            get => Mathf.Clamp01(Get(VolumeVoiceKey, 1f));
            set => Set(VolumeVoiceKey, Mathf.Clamp01(value));
        }

        // Locale code chosen in Settings, or empty to follow the platform
        // (Steam game language / device languages). Switching the running
        // language goes through Loc.SetLanguage, which saves the same key.
        public static string Language
        {
            get => PlayerPrefs.GetString(LanguageKey, Mejnun.Localization.LanguagePreference.FollowPlatform);
            set => Set(LanguageKey, value?.Trim() ?? Mejnun.Localization.LanguagePreference.FollowPlatform);
        }

        public static int QuickMatchBotFill
        {
            get
            {
                var value = PlayerPrefs.GetInt(QuickMatchBotFillKey, 30);
                return value == 0 || value == 30 ? value : 60;
            }
            set
            {
                var normalized = value == 0 || value == 30 ? value : 60;
                PlayerPrefs.SetInt(QuickMatchBotFillKey, normalized);
                PlayerPrefs.Save();
                Changed?.Invoke(QuickMatchBotFillKey);
            }
        }

        public static string OnlineNickname
        {
            get => PlayerPrefs.GetString(OnlineNicknameKey, string.Empty);
            set => Set(OnlineNicknameKey, value?.Trim() ?? string.Empty);
        }

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(ConfirmRevertKey);
            PlayerPrefs.DeleteKey(ConfirmDrawKey);
            PlayerPrefs.DeleteKey(ConfirmFixKey);
            PlayerPrefs.DeleteKey(ConfirmLeaveKey);
            PlayerPrefs.DeleteKey(HintBoardMovesKey);
            PlayerPrefs.DeleteKey(HintJiggleKey);
            PlayerPrefs.DeleteKey(CardHoverHighlightKey);
            PlayerPrefs.DeleteKey(ReducedMotionKey);
            PlayerPrefs.DeleteKey(HapticsKey);
            PlayerPrefs.DeleteKey(FourColourDeckKey);
            PlayerPrefs.DeleteKey(LeftHandedKey);
            PlayerPrefs.DeleteKey(BatterySaverKey);
            PlayerPrefs.DeleteKey(HandArcAmountKey);
            PlayerPrefs.DeleteKey(HandSpreadKey);
            PlayerPrefs.DeleteKey(HandCardScaleKey);
            PlayerPrefs.DeleteKey(HandLiftKey);
            PlayerPrefs.DeleteKey(OpponentCardScaleKey);
            PlayerPrefs.DeleteKey(HideOpponentHandsKey);
            PlayerPrefs.DeleteKey(EdgePanWidthKey);
            PlayerPrefs.DeleteKey(ZoomSensitivityKey);
            PlayerPrefs.DeleteKey(UiScaleKey);
            PlayerPrefs.DeleteKey(CpuSpeedKey);
            PlayerPrefs.DeleteKey(LegacyCpuSpeedKey);
            PlayerPrefs.DeleteKey(ShowFastForwardKey);
            PlayerPrefs.DeleteKey(OverlapSetsKey);
            PlayerPrefs.DeleteKey(OrganicBoardKey);
            PlayerPrefs.DeleteKey(BoardCardSpreadKey);
            PlayerPrefs.DeleteKey(CardMessinessKey);
            PlayerPrefs.DeleteKey(EasyOpeningDealKey);
            PlayerPrefs.DeleteKey(VolumeMasterKey);
            PlayerPrefs.DeleteKey(VolumeMusicKey);
            PlayerPrefs.DeleteKey(VolumeSfxKey);
            PlayerPrefs.DeleteKey(VolumeVoiceKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.DeleteKey(QuickMatchBotFillKey);
            PlayerPrefs.DeleteKey(OnlineNicknameKey);
            FastForward = false;
            Changed = null;
        }

        private static bool Get(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        private static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(key);
        }

        private static float Get(string key, float defaultValue)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke(key);
        }

        private static void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke(key);
        }
    }
}