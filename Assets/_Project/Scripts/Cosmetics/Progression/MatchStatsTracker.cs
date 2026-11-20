using System;
using System.Collections.Generic;
using System.Linq;
using Mejnun.Core;

namespace Mejnun.Cosmetics
{
    public sealed class MatchStatsTracker
    {
        private TableSnapshot turnStart;
        private TableSnapshot lastSnapshot;
        private bool inLocalTurn;
        private bool hintsUsed;
        private bool cleanSweep;
        private bool lastGasp;
        private bool royalRoad;
        private bool aceHigh;
        private bool bigEntrance;
        private bool architect;
        private bool surgeon;
        private bool borrowedTime;
        private bool seeingDouble;
        private bool coolUnderPressure;
        private int turnsPlayed;
        private int localTurns;
        private int draws;
        private int passes;
        private int timeouts;
        private int undosUsed;
        private int revertsUsed;
        private int fixBoardsUsed;
        private int cardsPlayedFromHand;
        private int setsCreated;
        private int straightsCreated;
        private int sameKindsCreated;
        private int longestStraight;
        private int mostCardsInOneTurn;
        private int goDowns;
        private int turnsToGoDown;
        private int largestHand;
        private int setsAtGoDown;
        private int turnSeconds;
        private float remainingSeconds = float.MaxValue;
        private int localSeat = -1;

        public void Reset(int configuredTurnSeconds = 0)
        {
            turnStart = null;
            lastSnapshot = null;
            inLocalTurn = false;
            hintsUsed = false;
            cleanSweep = false;
            lastGasp = false;
            royalRoad = false;
            aceHigh = false;
            bigEntrance = false;
            architect = false;
            surgeon = false;
            borrowedTime = false;
            seeingDouble = false;
            coolUnderPressure = false;
            turnsPlayed = 0;
            localTurns = 0;
            draws = 0;
            passes = 0;
            timeouts = 0;
            undosUsed = 0;
            revertsUsed = 0;
            fixBoardsUsed = 0;
            cardsPlayedFromHand = 0;
            setsCreated = 0;
            straightsCreated = 0;
            sameKindsCreated = 0;
            longestStraight = 0;
            mostCardsInOneTurn = 0;
            goDowns = 0;
            turnsToGoDown = 0;
            largestHand = 0;
            setsAtGoDown = 0;
            turnSeconds = Math.Max(0, configuredTurnSeconds);
            remainingSeconds = float.MaxValue;
            localSeat = -1;
        }

        public void Observe(GameState state, int observingLocalSeat, bool hintsOn)
        {
            if (state == null || observingLocalSeat < 0 || observingLocalSeat >= state.Hands.Count)
            {
                return;
            }

            var hand = new List<byte>(state.Hands[observingLocalSeat].Count);
            for (var index = 0; index < state.Hands[observingLocalSeat].Count; index++)
            {
                hand.Add(state.Hands[observingLocalSeat][index].Id);
            }

            var melds = new List<TableMeldSnapshot>(state.Board.Count);
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                var cardIds = new byte[meld.Cards.Count];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    cardIds[cardIndex] = meld.Cards[cardIndex].Id;
                }

                melds.Add(new TableMeldSnapshot(meld.Id, MeldValidator.Classify(meld.Cards).Kind, cardIds));
            }

            Observe(new TableSnapshot(observingLocalSeat, state.CurrentSeat, state.TurnNumber, hand,
                state.DrawPile.Count, state.PlayerPhases[observingLocalSeat] == PlayerPhase.Down, melds,
                state.Phase == GamePhase.Playing, state.ConsecutivePasses), hintsOn);
        }

        public void Observe(TableSnapshot snapshot, bool hintsOn)
        {
            if (snapshot == null)
            {
                return;
            }

            localSeat = snapshot.LocalSeat;
            hintsUsed |= hintsOn;
            largestHand = Math.Max(largestHand, snapshot.LocalHandCardIds.Count);

            if (snapshot.IsPlaying && snapshot.CurrentSeat == snapshot.LocalSeat)
            {
                if (!inLocalTurn || turnStart.TurnNumber != snapshot.TurnNumber)
                {
                    if (inLocalTurn)
                    {
                        FinalizeTurn(snapshot);
                    }

                    turnStart = snapshot;
                    inLocalTurn = true;
                    localTurns = Increment(localTurns);
                }

                lastSnapshot = snapshot;
                return;
            }

            if (inLocalTurn)
            {
                FinalizeTurn(snapshot);
            }

            lastSnapshot = snapshot;
        }

        public void NoteRemainingSeconds(float seconds)
        {
            if (!float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f)
            {
                remainingSeconds = seconds;
            }
        }

        public void RecordUndo()
        {
            undosUsed = Increment(undosUsed);
        }

        public void RecordRevert()
        {
            revertsUsed = Increment(revertsUsed);
        }

        public void RecordFixBoard()
        {
            fixBoardsUsed = Increment(fixBoardsUsed);
        }

        public void RecordTimeout()
        {
            timeouts = Increment(timeouts);
        }

        public MatchDetails Finish(GameResult result, GameState state)
        {
            if (state != null && localSeat >= 0)
            {
                Observe(state, localSeat, false);
            }

            return Finish(result, state, lastSnapshot);
        }

        public MatchDetails Finish(GameResult result, TableSnapshot finalSnapshot)
        {
            if (finalSnapshot != null)
            {
                Observe(finalSnapshot, false);
            }

            return Finish(result, null, finalSnapshot ?? lastSnapshot);
        }

        private MatchDetails Finish(GameResult result, GameState state, TableSnapshot finalSnapshot)
        {

            if (inLocalTurn && lastSnapshot != null)
            {
                FinalizeTurn(lastSnapshot);
            }

            var wonByEmptyingHand = result != null && result.Winner == localSeat &&
                                    result.Kind == GameEndKind.EmptyHand;
            var countWin = result != null && result.Winner == localSeat && result.Kind == GameEndKind.FewestCards;
            var winMargin = 0;
            if (countWin && state != null && localSeat >= 0 && localSeat < state.Hands.Count)
            {
                var localCount = state.Hands[localSeat].Count;
                var nextLowest = int.MaxValue;
                for (var seat = 0; seat < state.Hands.Count; seat++)
                {
                    if (seat != localSeat)
                    {
                        nextLowest = Math.Min(nextLowest, state.Hands[seat].Count);
                    }
                }

                if (nextLowest != int.MaxValue)
                {
                    winMargin = Math.Max(0, nextLowest - localCount);
                }
            }

            var drawPileEmpty = state != null
                ? state.DrawPile.Count == 0
                : finalSnapshot != null && finalSnapshot.DrawPileCount == 0;
            lastGasp |= wonByEmptyingHand && drawPileEmpty;
            return new MatchDetails
            {
                TurnsPlayed = turnsPlayed,
                Draws = draws,
                Passes = passes,
                Timeouts = timeouts,
                UndosUsed = undosUsed,
                RevertsUsed = revertsUsed,
                FixBoardsUsed = fixBoardsUsed,
                CardsPlayedFromHand = cardsPlayedFromHand,
                SetsCreated = setsCreated,
                StraightsCreated = straightsCreated,
                SameKindsCreated = sameKindsCreated,
                LongestStraight = longestStraight,
                MostCardsInOneTurn = mostCardsInOneTurn,
                GoDowns = goDowns,
                TurnsToGoDown = turnsToGoDown,
                LargestHand = largestHand,
                FastestWinTurns = result != null && result.Winner == localSeat ? localTurns : 0,
                WinMargin = winMargin,
                CountWin = countWin,
                TurnSeconds = turnSeconds,
                LocalTurns = localTurns,
                SetsAtGoDown = setsAtGoDown,
                CleanSweep = cleanSweep && wonByEmptyingHand,
                LastGasp = lastGasp,
                RoyalRoad = royalRoad,
                AceHigh = aceHigh,
                BigEntrance = bigEntrance,
                Architect = architect,
                Surgeon = surgeon,
                BorrowedTime = borrowedTime,
                SeeingDouble = seeingDouble,
                CoolUnderPressure = coolUnderPressure,
                HintsUsed = hintsUsed
            };
        }

        private void FinalizeTurn(TableSnapshot end)
        {
            if (turnStart == null || end == null)
            {
                inLocalTurn = false;
                return;
            }

            var cardsPlayed = CountPlayedFromHand(turnStart, end);
            var newMelds = GetNewMelds(turnStart.Board, end.Board);
            ObserveChangedBoard(turnStart.Board, end.Board);
            turnsPlayed = Increment(turnsPlayed);
            cardsPlayedFromHand = Add(cardsPlayedFromHand, cardsPlayed);
            mostCardsInOneTurn = Math.Max(mostCardsInOneTurn, cardsPlayed);
            architect |= cardsPlayed >= 8;
            setsCreated = Add(setsCreated, newMelds.Count);
            for (var index = 0; index < newMelds.Count; index++)
            {
                if (IsStraight(newMelds[index].Kind))
                {
                    straightsCreated = Increment(straightsCreated);
                }
                else if (newMelds[index].Kind == MeldKind.SameKind)
                {
                    sameKindsCreated = Increment(sameKindsCreated);
                }
            }

            if (turnStart.DrawPileCount > end.DrawPileCount && end.LocalHandCardIds.Count > turnStart.LocalHandCardIds.Count)
            {
                draws = Increment(draws);
            }

            if (end.ConsecutivePasses > turnStart.ConsecutivePasses)
            {
                passes = Add(passes, end.ConsecutivePasses - turnStart.ConsecutivePasses);
            }

            if (IsValidBoard(end.Board))
            {
                surgeon |= SplitStraight(turnStart.Board, end.Board);
                borrowedTime |= BorrowedFromFourSet(turnStart.Board, end.Board);
            }
            if (!turnStart.LocalIsDown && end.LocalIsDown)
            {
                goDowns = Increment(goDowns);
                if (turnsToGoDown == 0)
                {
                    turnsToGoDown = localTurns;
                }

                setsAtGoDown = Math.Max(setsAtGoDown, newMelds.Count);
                bigEntrance |= newMelds.Count >= 3;
                cleanSweep |= end.LocalHandCardIds.Count == 0;
            }

            if (remainingSeconds < 3f && IsValidBoard(end.Board))
            {
                coolUnderPressure = true;
            }

            remainingSeconds = float.MaxValue;
            turnStart = null;
            inLocalTurn = false;
        }

        private void ObserveChangedBoard(IReadOnlyList<TableMeldSnapshot> start, IReadOnlyList<TableMeldSnapshot> end)
        {
            for (var meldIndex = 0; meldIndex < end.Count; meldIndex++)
            {
                var meld = end[meldIndex];
                var previous = FindMeld(start, meld.Id);
                if (previous != null && SameMeld(previous, meld))
                {
                    continue;
                }

                if (IsStraight(meld.Kind))
                {
                    longestStraight = Math.Max(longestStraight, meld.CardIds.Count);
                    royalRoad |= meld.CardIds.Count >= 13;
                    aceHigh |= IsAceHigh(meld);
                }
            }

            seeingDouble |= !HasDoubleOnSeparateMelds(start) && HasDoubleOnSeparateMelds(end);
        }

        private static bool SameMeld(TableMeldSnapshot first, TableMeldSnapshot second)
        {
            if (first.Kind != second.Kind || first.CardIds.Count != second.CardIds.Count)
            {
                return false;
            }

            for (var index = 0; index < first.CardIds.Count; index++)
            {
                if (!second.CardIds.Contains(first.CardIds[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountPlayedFromHand(TableSnapshot start, TableSnapshot end)
        {
            var endingHand = new HashSet<byte>(end.LocalHandCardIds);
            var endingBoard = new HashSet<byte>();
            for (var meldIndex = 0; meldIndex < end.Board.Count; meldIndex++)
            {
                for (var cardIndex = 0; cardIndex < end.Board[meldIndex].CardIds.Count; cardIndex++)
                {
                    endingBoard.Add(end.Board[meldIndex].CardIds[cardIndex]);
                }
            }

            var count = 0;
            for (var index = 0; index < start.LocalHandCardIds.Count; index++)
            {
                var id = start.LocalHandCardIds[index];
                if (!endingHand.Contains(id) && endingBoard.Contains(id))
                {
                    count++;
                }
            }

            return count;
        }

        private static List<TableMeldSnapshot> GetNewMelds(IReadOnlyList<TableMeldSnapshot> start,
            IReadOnlyList<TableMeldSnapshot> end)
        {
            var ids = new HashSet<int>();
            for (var index = 0; index < start.Count; index++)
            {
                ids.Add(start[index].Id);
            }

            var added = new List<TableMeldSnapshot>();
            for (var index = 0; index < end.Count; index++)
            {
                if (!ids.Contains(end[index].Id))
                {
                    added.Add(end[index]);
                }
            }

            return added;
        }

        private static bool SplitStraight(IReadOnlyList<TableMeldSnapshot> start,
            IReadOnlyList<TableMeldSnapshot> end)
        {
            for (var index = 0; index < start.Count; index++)
            {
                var oldMeld = start[index];
                if (!IsStraight(oldMeld.Kind))
                {
                    continue;
                }

                var destinations = 0;
                for (var endIndex = 0; endIndex < end.Count; endIndex++)
                {
                    if (ContainsAny(end[endIndex], oldMeld.CardIds))
                    {
                        destinations++;
                    }
                }

                if (destinations >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BorrowedFromFourSet(IReadOnlyList<TableMeldSnapshot> start,
            IReadOnlyList<TableMeldSnapshot> end)
        {
            for (var index = 0; index < start.Count; index++)
            {
                var oldMeld = start[index];
                if (oldMeld.Kind != MeldKind.SameKind || oldMeld.CardIds.Count != 4)
                {
                    continue;
                }

                var newMeld = FindMeld(end, oldMeld.Id);
                for (var cardIndex = 0; cardIndex < oldMeld.CardIds.Count; cardIndex++)
                {
                    var cardId = oldMeld.CardIds[cardIndex];
                    if (newMeld != null && newMeld.CardIds.Contains(cardId))
                    {
                        continue;
                    }

                    for (var endIndex = 0; endIndex < end.Count; endIndex++)
                    {
                        if (end[endIndex].Id != oldMeld.Id && end[endIndex].CardIds.Contains(cardId))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasDoubleOnSeparateMelds(IReadOnlyList<TableMeldSnapshot> board)
        {
            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var first = board[meldIndex];
                for (var cardIndex = 0; cardIndex < first.CardIds.Count; cardIndex++)
                {
                    var id = first.CardIds[cardIndex];
                    for (var otherMeldIndex = meldIndex + 1; otherMeldIndex < board.Count; otherMeldIndex++)
                    {
                        var other = board[otherMeldIndex];
                        for (var otherCardIndex = 0; otherCardIndex < other.CardIds.Count; otherCardIndex++)
                        {
                            var otherId = other.CardIds[otherCardIndex];
                            if (id % Card.CardsPerDeck == otherId % Card.CardsPerDeck && id != otherId)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsAceHigh(TableMeldSnapshot meld)
        {
            if (meld.Kind != MeldKind.StraightHigh)
            {
                return false;
            }

            var queen = false;
            var king = false;
            var ace = false;
            for (var index = 0; index < meld.CardIds.Count; index++)
            {
                var rank = Card.FromId(meld.CardIds[index]).Rank;
                queen |= rank == Rank.Queen;
                king |= rank == Rank.King;
                ace |= rank == Rank.Ace;
            }

            return queen && king && ace;
        }

        private static bool ContainsAny(TableMeldSnapshot meld, IReadOnlyList<byte> cardIds)
        {
            for (var index = 0; index < cardIds.Count; index++)
            {
                if (meld.CardIds.Contains(cardIds[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static TableMeldSnapshot FindMeld(IReadOnlyList<TableMeldSnapshot> melds, int id)
        {
            for (var index = 0; index < melds.Count; index++)
            {
                if (melds[index].Id == id)
                {
                    return melds[index];
                }
            }

            return null;
        }

        private static bool IsValidBoard(IReadOnlyList<TableMeldSnapshot> board)
        {
            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].Kind == MeldKind.Invalid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStraight(MeldKind kind)
        {
            return kind == MeldKind.StraightLow || kind == MeldKind.StraightHigh;
        }

        private static int Increment(int value)
        {
            return value == int.MaxValue ? value : value + 1;
        }

        private static int Add(int value, int amount)
        {
            amount = Math.Max(0, amount);
            return value > int.MaxValue - amount ? int.MaxValue : value + amount;
        }
    }

    public sealed class TableSnapshot
    {
        public TableSnapshot(int localSeat, int currentSeat, int turnNumber, IEnumerable<byte> localHandCardIds,
            int drawPileCount, bool localIsDown, IEnumerable<TableMeldSnapshot> board, bool isPlaying = true,
            int consecutivePasses = 0)
        {
            LocalSeat = localSeat;
            CurrentSeat = currentSeat;
            TurnNumber = turnNumber;
            LocalHandCardIds = Copy(localHandCardIds);
            DrawPileCount = Math.Max(0, drawPileCount);
            LocalIsDown = localIsDown;
            Board = Copy(board);
            IsPlaying = isPlaying;
            ConsecutivePasses = Math.Max(0, consecutivePasses);
        }

        public int LocalSeat { get; }
        public int CurrentSeat { get; }
        public int TurnNumber { get; }
        public IReadOnlyList<byte> LocalHandCardIds { get; }
        public int DrawPileCount { get; }
        public bool LocalIsDown { get; }
        public IReadOnlyList<TableMeldSnapshot> Board { get; }
        public bool IsPlaying { get; }
        public int ConsecutivePasses { get; }

        private static byte[] Copy(IEnumerable<byte> values)
        {
            return values == null ? Array.Empty<byte>() : new List<byte>(values).ToArray();
        }

        private static TableMeldSnapshot[] Copy(IEnumerable<TableMeldSnapshot> values)
        {
            return values == null ? Array.Empty<TableMeldSnapshot>() : new List<TableMeldSnapshot>(values).ToArray();
        }
    }

    public sealed class TableMeldSnapshot
    {
        public TableMeldSnapshot(int id, MeldKind kind, IEnumerable<byte> cardIds)
        {
            Id = id;
            Kind = kind;
            CardIds = cardIds == null ? Array.Empty<byte>() : new List<byte>(cardIds).ToArray();
        }

        public int Id { get; }
        public MeldKind Kind { get; }
        public IReadOnlyList<byte> CardIds { get; }
    }
}