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
        private const string ReducedMotionKey = "settings.reducedMotion";
        private const string HapticsKey = "settings.haptics";
        private const string HandArcKey = "settings.hand.arc";
        private const string HandArcAmountKey = "settings.hand.arcAmount";
        private const string HandSpreadKey = "settings.hand.spread";
        private const string HandCardScaleKey = "settings.hand.cardScale";
        private const string HandLiftKey = "settings.hand.lift";
        private const string OpponentCardScaleKey = "settings.opponents.cardScale";
        private const string ZoomSensitivityKey = "settings.camera.zoomSensitivity";

        public static bool ConfirmRevert
        {
            get => Get(ConfirmRevertKey, true);
            set => Set(ConfirmRevertKey, value);
        }

        public static bool ConfirmDraw
        {
            get => Get(ConfirmDrawKey, true);
            set => Set(ConfirmDrawKey, value);
        }

        public static bool ConfirmFix
        {
            get => Get(ConfirmFixKey, true);
            set => Set(ConfirmFixKey, value);
        }

        public static bool ConfirmLeave
        {
            get => Get(ConfirmLeaveKey, true);
            set => Set(ConfirmLeaveKey, value);
        }

        public static bool HintBoardMoves
        {
            get => Get(HintBoardMovesKey, false);
            set => Set(HintBoardMovesKey, value);
        }

        public static bool HintJiggle
        {
            get => Get(HintJiggleKey, true);
            set => Set(HintJiggleKey, value);
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

        public static bool HandArc
        {
            get => Get(HandArcKey, true);
            set => Set(HandArcKey, value);
        }

        public static float HandArcAmount
        {
            get => Get(HandArcAmountKey, 0.5f);
            set => Set(HandArcAmountKey, value);
        }

        public static float HandSpread
        {
            get => Get(HandSpreadKey, 0.52f);
            set => Set(HandSpreadKey, value);
        }

        public static float HandCardScale
        {
            get => Get(HandCardScaleKey, 1f);
            set => Set(HandCardScaleKey, value);
        }

        public static float HandLift
        {
            get => Get(HandLiftKey, 0.15f);
            set => Set(HandLiftKey, value);
        }

        public static float OpponentCardScale
        {
            get => Get(OpponentCardScaleKey, 1f);
            set => Set(OpponentCardScaleKey, value);
        }

        public static float ZoomSensitivity
        {
            get => Get(ZoomSensitivityKey, 1f);
            set => Set(ZoomSensitivityKey, value);
        }

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(ConfirmRevertKey);
            PlayerPrefs.DeleteKey(ConfirmDrawKey);
            PlayerPrefs.DeleteKey(ConfirmFixKey);
            PlayerPrefs.DeleteKey(ConfirmLeaveKey);
            PlayerPrefs.DeleteKey(HintBoardMovesKey);
            PlayerPrefs.DeleteKey(HintJiggleKey);
            PlayerPrefs.DeleteKey(ReducedMotionKey);
            PlayerPrefs.DeleteKey(HapticsKey);
            PlayerPrefs.DeleteKey(HandArcKey);
            PlayerPrefs.DeleteKey(HandArcAmountKey);
            PlayerPrefs.DeleteKey(HandSpreadKey);
            PlayerPrefs.DeleteKey(HandCardScaleKey);
            PlayerPrefs.DeleteKey(HandLiftKey);
            PlayerPrefs.DeleteKey(OpponentCardScaleKey);
            PlayerPrefs.DeleteKey(ZoomSensitivityKey);
        }

        private static bool Get(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        private static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static float Get(string key, float defaultValue)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}