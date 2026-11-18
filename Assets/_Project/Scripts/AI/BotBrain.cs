using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    public sealed class BotTurnPlan
    {
        internal BotTurnPlan(List<GameCommand> commands, int cardsPlayed, string fallbackReason)
        {
            Commands = commands.AsReadOnly();
            CardsPlayed = cardsPlayed;
            FallbackReason = fallbackReason;
        }

        /// <summary>Commands for the current seat, in order. They end with EndTurn or DrawOrPass unless the bot wins.</summary>
        public IReadOnlyList<GameCommand> Commands { get; }

        public int CardsPlayed { get; }

        /// <summary>Set when the planned play could not be executed and the bot drew instead (a bug signal).</summary>
        public string FallbackReason { get; }

        public bool Draws => Commands.Count > 0 && Commands[Commands.Count - 1].Type == CommandType.DrawOrPass;
    }

    /// <summary>
    /// Plans one bot turn. The work runs on a private copy of the game (made from a snapshot), so it is safe to
    /// call from a worker thread. Every command is validated by the engine on that copy before it is returned.
    /// </summary>
    public static class BotBrain
    {
        public static BotTurnPlan PlanTurn(MejnunGame game, BotDifficulty difficulty, ulong randomSeed,
            BotStyle style = BotStyle.Balanced, BotKnowledge knowledge = null)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            return PlanTurn(SnapshotSerializer.Write(game), difficulty, randomSeed, style, knowledge);
        }

        public static BotTurnPlan PlanTurn(byte[] snapshot, BotDifficulty difficulty, ulong randomSeed,
            BotStyle style = BotStyle.Balanced, BotKnowledge knowledge = null)
        {
            var scratch = SnapshotSerializer.Read(snapshot);
            var state = scratch.State;
            var commands = new List<GameCommand>();
            if (state.Phase != GamePhase.Playing || scratch.Turn == null)
            {
                return new BotTurnPlan(commands, 0, null);
            }

            var seat = state.CurrentSeat;
            if (scratch.Turn.Moves.Count > 0)
            {
                var revert = GameCommand.Revert(seat);
                scratch.Apply(revert);
                commands.Add(revert);
            }

            var handBefore = state.Hands[seat].Count;
            var random = new Pcg32(randomSeed);
            var strategyDifficulty = style == BotStyle.Blocker ? BotDifficulty.Hard : difficulty;
            var intent = BotStrategies.Plan(strategyDifficulty, BotContext.From(state, seat), random);
            if (intent.IsDraw)
            {
                return Draw(seat, null);
            }

            if (style == BotStyle.HoldBack && random.NextInt(100) < 30)
            {
                return Draw(seat, null);
            }

            if (style == BotStyle.Blocker && intent.FinalBoard != null && knowledge != null &&
                !knowledge.WouldEnableKnownExtender(ToBoardCards(state.Board), seat) &&
                knowledge.WouldEnableKnownExtender(intent.FinalBoard, seat))
            {
                return Draw(seat, null);
            }

            if (!TargetBoardExecutor.PlaceNewMelds(scratch, seat, intent.GoDownMelds, commands))
            {
                return Draw(seat, "Could not place the go-down sets.");
            }

            if (intent.FinalBoard != null && state.Phase == GamePhase.Playing)
            {
                if (!TargetBoardExecutor.Rearrange(scratch, seat, intent.FinalBoard, commands))
                {
                    return Draw(seat, "Could not rearrange the board.");
                }
            }

            if (state.Phase == GamePhase.Finished)
            {
                return new BotTurnPlan(commands, handBefore - state.Hands[seat].Count, null);
            }

            var endTurn = GameCommand.EndTurn(seat);
            if (!scratch.Apply(endTurn).Ok)
            {
                return Draw(seat, "The planned board could not end the turn.");
            }

            commands.Add(endTurn);
            return new BotTurnPlan(commands, handBefore - state.Hands[seat].Count, null);
        }

        private static BotTurnPlan Draw(int seat, string reason)
        {
            return new BotTurnPlan(new List<GameCommand> { GameCommand.DrawOrPass(seat) }, 0, reason);
        }

        private static List<IReadOnlyList<Card>> ToBoardCards(IReadOnlyList<Meld> board)
        {
            var melds = new List<IReadOnlyList<Card>>(board.Count);
            for (var index = 0; index < board.Count; index++)
            {
                melds.Add(board[index].Cards);
            }

            return melds;
        }
    }
}
