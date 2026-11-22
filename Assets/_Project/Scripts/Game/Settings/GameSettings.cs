using UnityEngine;

namespace Mejnun.Game
{
    public static class GameSettings
    {
        private const string ConfirmRevertKey = "settings.confirm.revert";
        private const string ConfirmDrawKey = "settings.confirm.draw";
        private const string ConfirmFixKey = "settings.confirm.fix";
        private const string ConfirmLeaveKey = "settings.confirm.leave";
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
        public const string EdgeScrollWidthKey = "settings.camera.edgeScrollWidth";
        public const string EdgePanSpeedKey = "settings.camera.edgePanSpeed";
        public const string LongPressSecondsKey = "settings.input.longPress";
        public const string TouchPanStartSpeedKey = "settings.input.panStartSpeed";
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
            get => Get(ConfirmRevertKey, SettingsDefaults.Active.ConfirmRevert);
            set => Set(ConfirmRevertKey, value);
        }

        public static bool ConfirmDraw
        {
            get => Get(ConfirmDrawKey, SettingsDefaults.Active.ConfirmDraw);
            set => Set(ConfirmDrawKey, value);
        }

        public static bool ConfirmFix
        {
            get => Get(ConfirmFixKey, SettingsDefaults.Active.ConfirmFix);
            set => Set(ConfirmFixKey, value);
        }

        public static bool ConfirmLeave
        {
            get => Get(ConfirmLeaveKey, SettingsDefaults.Active.ConfirmLeave);
            set => Set(ConfirmLeaveKey, value);
        }

        public static bool HintJiggle
        {
            get => Get(HintJiggleKey, SettingsDefaults.Active.HintJiggle);
            set => Set(HintJiggleKey, value);
        }

        public static bool CardHoverHighlight
        {
            get => Get(CardHoverHighlightKey, SettingsDefaults.Active.CardHoverHighlight);
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
            get => Get(ReducedMotionKey, SettingsDefaults.Active.ReducedMotion);
            set => Set(ReducedMotionKey, value);
        }

        public static bool HapticsEnabled
        {
            get => Get(HapticsKey, SettingsDefaults.Active.HapticsEnabled);
            set => Set(HapticsKey, value);
        }

        public static bool FourColourDeck
        {
            get => Get(FourColourDeckKey, SettingsDefaults.Active.FourColourDeck);
            set => Set(FourColourDeckKey, value);
        }

        public static bool LeftHanded
        {
            get => Get(LeftHandedKey, SettingsDefaults.Active.LeftHanded);
            set => Set(LeftHandedKey, value);
        }

        public static bool BatterySaver
        {
            get => Get(BatterySaverKey, SettingsDefaults.Active.BatterySaver);
            set => Set(BatterySaverKey, value);
        }

        public static float HandArcAmount
        {
            get => Get(HandArcAmountKey, SettingsDefaults.Active.HandArcAmount);
            set => Set(HandArcAmountKey, value);
        }

        public static float HandSpread
        {
            get => Get(HandSpreadKey, SettingsDefaults.Active.HandSpread);
            set => Set(HandSpreadKey, value);
        }

        public static float HandCardScale
        {
            get => Get(HandCardScaleKey, SettingsDefaults.Active.HandCardScale);
            set => Set(HandCardScaleKey, value);
        }

        public static float HandLift
        {
            get => Get(HandLiftKey, SettingsDefaults.Active.HandLift);
            set => Set(HandLiftKey, value);
        }

        public static float OpponentCardScale
        {
            get => Get(OpponentCardScaleKey, SettingsDefaults.Active.OpponentCardScale);
            set => Set(OpponentCardScaleKey, value);
        }

        public static bool HideOpponentHands
        {
            get => Get(HideOpponentHandsKey, DeviceProfile.IsMobilePlatform
                ? SettingsDefaults.Active.HideOpponentHandsMobile
                : SettingsDefaults.Active.HideOpponentHandsDesktop);
            set => Set(HideOpponentHandsKey, value);
        }

        public static float EdgePanWidth
        {
            get => Mathf.Clamp(Get(EdgePanWidthKey, DefaultEdgePanWidth), 0f, 0.25f);
            set => Set(EdgePanWidthKey, Mathf.Clamp(value, 0f, 0.25f));
        }

        public static float DefaultEdgePanWidth => DeviceProfile.IsMobilePlatform
            ? SettingsDefaults.Active.EdgePanWidthMobile
            : SettingsDefaults.Active.EdgePanWidthDesktop;

        public static float EdgeScrollWidth
        {
            get => Mathf.Clamp(Get(EdgeScrollWidthKey, DefaultEdgeScrollWidth), 0f, 0.25f);
            set => Set(EdgeScrollWidthKey, Mathf.Clamp(value, 0f, 0.25f));
        }

        public static float DefaultEdgeScrollWidth => DeviceProfile.IsMobilePlatform
            ? SettingsDefaults.Active.EdgeScrollWidthMobile
            : SettingsDefaults.Active.EdgeScrollWidthDesktop;

        public static float EdgePanSpeed
        {
            get => Mathf.Clamp(Get(EdgePanSpeedKey, SettingsDefaults.Active.EdgePanSpeed), 0.25f, 3f);
            set => Set(EdgePanSpeedKey, Mathf.Clamp(value, 0.25f, 3f));
        }

        public static float LongPressSeconds
        {
            get => Mathf.Clamp(Get(LongPressSecondsKey, SettingsDefaults.Active.LongPressSeconds), 0.1f, 0.8f);
            set => Set(LongPressSecondsKey, Mathf.Clamp(value, 0.1f, 0.8f));
        }

        public static float TouchPanStartSpeed
        {
            get => Mathf.Clamp(Get(TouchPanStartSpeedKey, SettingsDefaults.Active.TouchPanStartSpeed), 50f, 400f);
            set => Set(TouchPanStartSpeedKey, Mathf.Clamp(value, 50f, 400f));
        }

        public static float ZoomSensitivity
        {
            get => Get(ZoomSensitivityKey, SettingsDefaults.Active.ZoomSensitivity);
            set => Set(ZoomSensitivityKey, value);
        }

        public static float UiScale
        {
            get => Mathf.Clamp(Get(UiScaleKey, DeviceProfile.IsMobilePlatform
                ? SettingsDefaults.Active.UiScaleMobile
                : SettingsDefaults.Active.UiScaleDesktop), 0.8f, 3f);
            set => Set(UiScaleKey, Mathf.Clamp(value, 0.8f, 3f));
        }

        public static int CpuSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(CpuSpeedKey, SettingsDefaults.Active.CpuSpeed), 0, 5);
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
            get => Get(ShowFastForwardKey, SettingsDefaults.Active.ShowFastForward);
            set => Set(ShowFastForwardKey, value);
        }

        public static bool OverlapSets
        {
            get => Get(OverlapSetsKey, SettingsDefaults.Active.OverlapSets);
            set => Set(OverlapSetsKey, value);
        }

        public static bool OrganicBoard
        {
            get => Get(OrganicBoardKey, SettingsDefaults.Active.OrganicBoard);
            set => Set(OrganicBoardKey, value);
        }

        public static float BoardCardSpread
        {
            get => Mathf.Clamp(Get(BoardCardSpreadKey, SettingsDefaults.Active.BoardCardSpread), 0.5f, 1.5f);
            set => Set(BoardCardSpreadKey, Mathf.Clamp(value, 0.5f, 1.5f));
        }

        public static float CardMessiness
        {
            get => Mathf.Clamp01(Get(CardMessinessKey, SettingsDefaults.Active.CardMessiness));
            set => Set(CardMessinessKey, Mathf.Clamp01(value));
        }

        public static bool EasyOpeningDeal
        {
            get => Get(EasyOpeningDealKey, SettingsDefaults.Active.EasyOpeningDeal);
            set => Set(EasyOpeningDealKey, value);
        }

        public static bool FastForward
        {
            get => fastForward;
            set => fastForward = value;
        }

        public static float VolumeMaster
        {
            get => Mathf.Clamp01(Get(VolumeMasterKey, SettingsDefaults.Active.VolumeMaster));
            set => Set(VolumeMasterKey, Mathf.Clamp01(value));
        }

        public static float VolumeMusic
        {
            get => Mathf.Clamp01(Get(VolumeMusicKey, SettingsDefaults.Active.VolumeMusic));
            set => Set(VolumeMusicKey, Mathf.Clamp01(value));
        }

        public static float VolumeSfx
        {
            get => Mathf.Clamp01(Get(VolumeSfxKey, SettingsDefaults.Active.VolumeSfx));
            set => Set(VolumeSfxKey, Mathf.Clamp01(value));
        }

        public static float VolumeVoice
        {
            get => Mathf.Clamp01(Get(VolumeVoiceKey, SettingsDefaults.Active.VolumeVoice));
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
                var value = PlayerPrefs.GetInt(QuickMatchBotFillKey, SettingsDefaults.Active.QuickMatchBotFill);
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

        public static void ResetToDefaults()
        {
            var changedKeys = new[]
            {
                ConfirmRevertKey, ConfirmDrawKey, ConfirmFixKey, ConfirmLeaveKey,
                HintJiggleKey, CardHoverHighlightKey, ReducedMotionKey, HapticsKey,
                FourColourDeckKey, LeftHandedKey, BatterySaverKey,
                HandArcAmountKey, HandSpreadKey, HandCardScaleKey, HandLiftKey,
                OpponentCardScaleKey, HideOpponentHandsKey, EdgePanWidthKey, EdgeScrollWidthKey, EdgePanSpeedKey,
                LongPressSecondsKey,
                TouchPanStartSpeedKey,
                ZoomSensitivityKey, UiScaleKey, CpuSpeedKey, LegacyCpuSpeedKey, ShowFastForwardKey,
                OverlapSetsKey, OrganicBoardKey, BoardCardSpreadKey, CardMessinessKey, EasyOpeningDealKey,
                VolumeMasterKey, VolumeMusicKey, VolumeSfxKey, VolumeVoiceKey, QuickMatchBotFillKey
            };

            for (var index = 0; index < changedKeys.Length; index++)
            {
                PlayerPrefs.DeleteKey(changedKeys[index]);
            }

            PlayerPrefs.Save();
            for (var index = 0; index < changedKeys.Length; index++)
            {
                Changed?.Invoke(changedKeys[index]);
            }
        }

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(ConfirmRevertKey);
            PlayerPrefs.DeleteKey(ConfirmDrawKey);
            PlayerPrefs.DeleteKey(ConfirmFixKey);
            PlayerPrefs.DeleteKey(ConfirmLeaveKey);
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
            PlayerPrefs.DeleteKey(EdgeScrollWidthKey);
            PlayerPrefs.DeleteKey(EdgePanSpeedKey);
            PlayerPrefs.DeleteKey(LongPressSecondsKey);
            PlayerPrefs.DeleteKey(TouchPanStartSpeedKey);
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