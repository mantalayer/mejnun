using System;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [Serializable]
    public sealed class PlayerStats
    {
        [SerializeField] private int played;
        [SerializeField] private int wins;
        [SerializeField] private int mejnunWins;
        [SerializeField] private int stalemates;

        public int Played => played;
        public int Wins => wins;
        public int MejnunWins => mejnunWins;
        public int Stalemates => stalemates;

        internal void RecordMatch(MatchSummary summary)
        {
            played = Increment(played);
            if (summary.CanAwardWin && summary.LocalWin)
            {
                wins = Increment(wins);
                if (summary.MejnunWin)
                {
                    mejnunWins = Increment(mejnunWins);
                }
            }

            if (summary.Stalemate)
            {
                stalemates = Increment(stalemates);
            }
        }

        internal void Normalize()
        {
            played = Math.Max(0, played);
            wins = Math.Max(0, wins);
            mejnunWins = Math.Max(0, mejnunWins);
            stalemates = Math.Max(0, stalemates);
        }

        private static int Increment(int value)
        {
            return value == int.MaxValue ? value : value + 1;
        }
    }
}