using System;

namespace Mejnun.Cosmetics
{
    public enum MatchMode
    {
        Solo,
        PassAndPlay
    }

    public readonly struct MatchSummary
    {
        public MatchSummary(MatchMode mode, int seatCount, int humanCount, bool localWin, bool mejnunWin,
            bool stalemate, bool beatHardBot)
        {
            if (!Enum.IsDefined(typeof(MatchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown match mode.");
            }

            if (seatCount < 2 || seatCount > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount,
                    "A match must have between two and six seats.");
            }

            if (humanCount < 1 || humanCount > seatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(humanCount), humanCount,
                    "A match must have at least one human seat.");
            }

            Mode = mode;
            SeatCount = seatCount;
            HumanCount = humanCount;
            LocalWin = localWin;
            MejnunWin = localWin && mejnunWin;
            Stalemate = stalemate;
            BeatHardBot = localWin && mode == MatchMode.Solo && beatHardBot;
        }

        public MatchMode Mode { get; }
        public int SeatCount { get; }
        public int HumanCount { get; }
        public bool LocalWin { get; }
        public bool MejnunWin { get; }
        public bool Stalemate { get; }
        public bool BeatHardBot { get; }
        public bool CanAwardWin => Mode == MatchMode.Solo || HumanCount < 2;
    }
}