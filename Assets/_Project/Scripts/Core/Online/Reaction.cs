using System;

namespace Mejnun.Core
{
    public enum Reaction : byte
    {
        HurryUp,
        MessedMeUp,
        GoDown,
        CanYouFinish,
        FinishThreat,
        NiceMove,
        ThankYou
    }

    public static class ReactionRules
    {
        public static bool IsBroadcast(Reaction reaction)
        {
            return reaction == Reaction.MessedMeUp || reaction == Reaction.CanYouFinish ||
                   reaction == Reaction.FinishThreat || IsPositive(reaction);
        }

        public static bool IsPositive(Reaction reaction)
        {
            return reaction == Reaction.NiceMove || reaction == Reaction.ThankYou;
        }

        public static bool IsTargeted(Reaction reaction)
        {
            return Enum.IsDefined(typeof(Reaction), reaction) && !IsBroadcast(reaction);
        }

        public static double DelaySeconds(Reaction reaction)
        {
            return reaction == Reaction.HurryUp ? 20 : 0;
        }

        public static double CooldownSeconds(Reaction reaction)
        {
            return Enum.IsDefined(typeof(Reaction), reaction) ? 20 : 0;
        }

        public static bool CanSend(Reaction reaction, GameState state, int senderSeat)
        {
            if (!Enum.IsDefined(typeof(Reaction), reaction) || state == null ||
                state.Phase != GamePhase.Playing || senderSeat < 0 || senderSeat >= state.Hands.Count ||
                state.CurrentSeat < 0 || state.CurrentSeat >= state.Hands.Count ||
                state.PlayerPhases.Count != state.Hands.Count)
            {
                return false;
            }

            var targetSeat = state.CurrentSeat;
            switch (reaction)
            {
                case Reaction.HurryUp:
                    return targetSeat != senderSeat;
                case Reaction.MessedMeUp:
                case Reaction.NiceMove:
                case Reaction.ThankYou:
                    return true;
                case Reaction.GoDown:
                    return targetSeat != senderSeat && state.PlayerPhases[targetSeat] == PlayerPhase.NotDown;
                case Reaction.CanYouFinish:
                    return targetSeat != senderSeat && state.Hands[targetSeat].Count == 1;
                case Reaction.FinishThreat:
                    return targetSeat != senderSeat && state.Hands[senderSeat].Count == 1 &&
                           state.Hands[targetSeat].Count == 1;
                default:
                    return false;
            }
        }
    }
}