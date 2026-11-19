using System;

namespace Mejnun.Cosmetics
{
    public enum MatchMode
    {
        Solo,
        PassAndPlay,
        Tutorial
    }

    public readonly struct MatchSummary
    {
        public MatchSummary(MatchMode mode, int seatCount, int humanCount, bool localWin, bool mejnunWin,
            bool stalemate, bool beatHardBot, int cpuDifficulty = 2, int durationSeconds = 0)
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

            if (cpuDifficulty < 0 || cpuDifficulty > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(cpuDifficulty), cpuDifficulty,
                    "CPU difficulty must be between zero and two.");
            }

            if (durationSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds,
                    "Match duration cannot be negative.");
            }

            Mode = mode;
            SeatCount = seatCount;
            HumanCount = humanCount;
            LocalWin = localWin;
            MejnunWin = localWin && mejnunWin;
            Stalemate = stalemate;
            BeatHardBot = localWin && mode == MatchMode.Solo && beatHardBot;
            CpuDifficulty = cpuDifficulty;
            DurationSeconds = durationSeconds;
        }

        public MatchMode Mode { get; }
        public int SeatCount { get; }
        public int HumanCount { get; }
        public bool LocalWin { get; }
        public bool MejnunWin { get; }
        public bool Stalemate { get; }
        public bool BeatHardBot { get; }
        public int CpuDifficulty { get; }
        public int DurationSeconds { get; }
        public bool CanAwardWin => Mode == MatchMode.Solo || HumanCount < 2;
    }
}