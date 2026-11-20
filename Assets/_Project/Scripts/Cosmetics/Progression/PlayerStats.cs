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
        [SerializeField] private int countWins;
        [SerializeField] private int gamesPlayedSolo;
        [SerializeField] private int gamesPlayedOnline;
        [SerializeField] private int gamesPlayedPassAndPlay;
        [SerializeField] private int winsVsEasy;
        [SerializeField] private int winsVsMedium;
        [SerializeField] private int winsVsHard;
        [SerializeField] private int onlineWins;
        [SerializeField] private int currentWinStreak;
        [SerializeField] private int bestWinStreak;
        [SerializeField] private int currentBotWinStreak;
        [SerializeField] private int bestBotWinStreak;
        [SerializeField] private int cardsPlayedFromHand;
        [SerializeField] private int setsCreated;
        [SerializeField] private int straightsCreated;
        [SerializeField] private int sameKindsCreated;
        [SerializeField] private int longestStraight;
        [SerializeField] private int mostCardsInOneTurn;
        [SerializeField] private int goDowns;
        [SerializeField] private int totalTurnsToGoDown;
        [SerializeField] private int turnsPlayed;
        [SerializeField] private int draws;
        [SerializeField] private int passes;
        [SerializeField] private int timeouts;
        [SerializeField] private int undosUsed;
        [SerializeField] private int revertsUsed;
        [SerializeField] private int fixBoardsUsed;
        [SerializeField] private int fastestWinTurns;
        [SerializeField] private int fastestWinSeconds;
        [SerializeField] private int largestHandAtWin;
        [SerializeField] private int totalPlaySeconds;
        [SerializeField] private int onlineGamesHosted;
        [SerializeField] private int hotSeatQuestionCount;

        public int Played => played;
        public int Wins => wins;
        public int MejnunWins => mejnunWins;
        public int Stalemates => stalemates;
        public int CountWins => countWins;
        public int GamesPlayedSolo => gamesPlayedSolo;
        public int GamesPlayedOnline => gamesPlayedOnline;
        public int GamesPlayedPassAndPlay => gamesPlayedPassAndPlay;
        public int WinsVsEasy => winsVsEasy;
        public int WinsVsMedium => winsVsMedium;
        public int WinsVsHard => winsVsHard;
        public int OnlineWins => onlineWins;
        public int CurrentWinStreak => currentWinStreak;
        public int BestWinStreak => bestWinStreak;
        public int CurrentBotWinStreak => currentBotWinStreak;
        public int BestBotWinStreak => bestBotWinStreak;
        public int CardsPlayedFromHand => cardsPlayedFromHand;
        public int SetsCreated => setsCreated;
        public int StraightsCreated => straightsCreated;
        public int SameKindsCreated => sameKindsCreated;
        public int LongestStraight => longestStraight;
        public int MostCardsInOneTurn => mostCardsInOneTurn;
        public int GoDowns => goDowns;
        public int AverageTurnsToGoDown => goDowns == 0
            ? 0
            : (int)(((long)totalTurnsToGoDown + goDowns / 2L) / goDowns);
        public int TurnsPlayed => turnsPlayed;
        public int Draws => draws;
        public int Passes => passes;
        public int Timeouts => timeouts;
        public int UndosUsed => undosUsed;
        public int RevertsUsed => revertsUsed;
        public int FixBoardsUsed => fixBoardsUsed;
        public int FastestWinTurns => fastestWinTurns;
        public int FastestWinSeconds => fastestWinSeconds;
        public int LargestHandAtWin => largestHandAtWin;
        public int TotalPlaySeconds => totalPlaySeconds;
        public int OnlineGamesHosted => onlineGamesHosted;
        public int HotSeatQuestionCount => hotSeatQuestionCount;

        internal void RecordMatch(MatchSummary summary)
        {
            played = Increment(played);
            if (summary.CanAwardWin && summary.LocalWin && !summary.Stalemate)
            {
                wins = Increment(wins);
                if (summary.MejnunWin)
                {
                    mejnunWins = Increment(mejnunWins);
                }

                if (summary.Details.CountWin)
                {
                    countWins = Increment(countWins);
                }

                if (summary.Mode == MatchMode.Online)
                {
                    onlineWins = Increment(onlineWins);
                }

                if (summary.Mode == MatchMode.Solo)
                {
                    switch (summary.CpuDifficulty)
                    {
                        case 0:
                            winsVsEasy = Increment(winsVsEasy);
                            break;
                        case 1:
                            winsVsMedium = Increment(winsVsMedium);
                            break;
                        default:
                            winsVsHard = Increment(winsVsHard);
                            break;
                    }
                }
            }

            if (summary.Stalemate)
            {
                stalemates = Increment(stalemates);
            }

            switch (summary.Mode)
            {
                case MatchMode.Solo:
                    gamesPlayedSolo = Increment(gamesPlayedSolo);
                    break;
                case MatchMode.Online:
                    gamesPlayedOnline = Increment(gamesPlayedOnline);
                    break;
                case MatchMode.PassAndPlay:
                    gamesPlayedPassAndPlay = Increment(gamesPlayedPassAndPlay);
                    break;
            }

            var details = summary.Details;
            cardsPlayedFromHand = Add(cardsPlayedFromHand, details.CardsPlayedFromHand);
            setsCreated = Add(setsCreated, details.SetsCreated);
            straightsCreated = Add(straightsCreated, details.StraightsCreated);
            sameKindsCreated = Add(sameKindsCreated, details.SameKindsCreated);
            longestStraight = Math.Max(longestStraight, details.LongestStraight);
            mostCardsInOneTurn = Math.Max(mostCardsInOneTurn, details.MostCardsInOneTurn);
            goDowns = Add(goDowns, details.GoDowns);
            totalTurnsToGoDown = Add(totalTurnsToGoDown, details.TurnsToGoDown);
            turnsPlayed = Add(turnsPlayed, details.TurnsPlayed);
            draws = Add(draws, details.Draws);
            passes = Add(passes, details.Passes);
            timeouts = Add(timeouts, details.Timeouts);
            undosUsed = Add(undosUsed, details.UndosUsed);
            revertsUsed = Add(revertsUsed, details.RevertsUsed);
            fixBoardsUsed = Add(fixBoardsUsed, details.FixBoardsUsed);
            totalPlaySeconds = Add(totalPlaySeconds, summary.DurationSeconds);

            if (summary.Mode == MatchMode.Online && details.IsHost)
            {
                onlineGamesHosted = Increment(onlineGamesHosted);
            }

            if (details.ChosenFirstByQuestion)
            {
                hotSeatQuestionCount = Increment(hotSeatQuestionCount);
            }

            UpdateStreaks(summary);
            if (summary.CanAwardWin && summary.LocalWin && !summary.Stalemate)
            {
                if (details.FastestWinTurns > 0 &&
                    (fastestWinTurns == 0 || details.FastestWinTurns < fastestWinTurns))
                {
                    fastestWinTurns = details.FastestWinTurns;
                }

                if (summary.DurationSeconds > 0 &&
                    (fastestWinSeconds == 0 || summary.DurationSeconds < fastestWinSeconds))
                {
                    fastestWinSeconds = summary.DurationSeconds;
                }

                largestHandAtWin = Math.Max(largestHandAtWin, details.LargestHand);
            }
        }

        internal void Normalize()
        {
            played = Math.Max(0, played);
            wins = Math.Max(0, wins);
            mejnunWins = Math.Max(0, mejnunWins);
            stalemates = Math.Max(0, stalemates);
            countWins = Math.Max(0, countWins);
            gamesPlayedSolo = Math.Max(0, gamesPlayedSolo);
            gamesPlayedOnline = Math.Max(0, gamesPlayedOnline);
            gamesPlayedPassAndPlay = Math.Max(0, gamesPlayedPassAndPlay);
            winsVsEasy = Math.Max(0, winsVsEasy);
            winsVsMedium = Math.Max(0, winsVsMedium);
            winsVsHard = Math.Max(0, winsVsHard);
            onlineWins = Math.Max(0, onlineWins);
            currentWinStreak = Math.Max(0, currentWinStreak);
            bestWinStreak = Math.Max(currentWinStreak, Math.Max(0, bestWinStreak));
            currentBotWinStreak = Math.Max(0, currentBotWinStreak);
            bestBotWinStreak = Math.Max(currentBotWinStreak, Math.Max(0, bestBotWinStreak));
            cardsPlayedFromHand = Math.Max(0, cardsPlayedFromHand);
            setsCreated = Math.Max(0, setsCreated);
            straightsCreated = Math.Max(0, straightsCreated);
            sameKindsCreated = Math.Max(0, sameKindsCreated);
            longestStraight = Math.Max(0, longestStraight);
            mostCardsInOneTurn = Math.Max(0, mostCardsInOneTurn);
            goDowns = Math.Max(0, goDowns);
            totalTurnsToGoDown = Math.Max(0, totalTurnsToGoDown);
            turnsPlayed = Math.Max(0, turnsPlayed);
            draws = Math.Max(0, draws);
            passes = Math.Max(0, passes);
            timeouts = Math.Max(0, timeouts);
            undosUsed = Math.Max(0, undosUsed);
            revertsUsed = Math.Max(0, revertsUsed);
            fixBoardsUsed = Math.Max(0, fixBoardsUsed);
            fastestWinTurns = Math.Max(0, fastestWinTurns);
            fastestWinSeconds = Math.Max(0, fastestWinSeconds);
            largestHandAtWin = Math.Max(0, largestHandAtWin);
            totalPlaySeconds = Math.Max(0, totalPlaySeconds);
            onlineGamesHosted = Math.Max(0, onlineGamesHosted);
            hotSeatQuestionCount = Math.Max(0, hotSeatQuestionCount);
        }

        private void UpdateStreaks(MatchSummary summary)
        {
            if (!summary.CanAwardWin || summary.Stalemate)
            {
                return;
            }

            if (summary.LocalWin)
            {
                currentWinStreak = Increment(currentWinStreak);
                bestWinStreak = Math.Max(bestWinStreak, currentWinStreak);
            }
            else
            {
                currentWinStreak = 0;
            }

            if (summary.Mode == MatchMode.Solo && summary.LocalWin)
            {
                currentBotWinStreak = Increment(currentBotWinStreak);
                bestBotWinStreak = Math.Max(bestBotWinStreak, currentBotWinStreak);
            }
            else
            {
                currentBotWinStreak = 0;
            }
        }

        private static int Add(int value, int amount)
        {
            amount = Math.Max(0, amount);
            return value > int.MaxValue - amount ? int.MaxValue : value + amount;
        }

        private static int Increment(int value)
        {
            return value == int.MaxValue ? value : value + 1;
        }
    }
}