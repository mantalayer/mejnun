using System;

namespace Mejnun.Core
{
    public readonly struct GameCommand
    {
        private readonly byte[] cardIds;

        public GameCommand(CommandType type, int seat)
            : this(type, seat, null, default, 0, 0, 0)
        {
        }

        public GameCommand(CommandType type, int seat, byte[] cardIds, CardTarget target,
            int meldId, int otherMeldId, int index)
        {
            Type = type;
            Seat = seat;
            this.cardIds = cardIds == null ? Array.Empty<byte>() : (byte[])cardIds.Clone();
            Target = target;
            MeldId = meldId;
            OtherMeldId = otherMeldId;
            Index = index;
        }

        public CommandType Type { get; }
        public int Seat { get; }
        public byte[] CardIds => cardIds == null ? Array.Empty<byte>() : (byte[])cardIds.Clone();
        public CardTarget Target { get; }
        public int MeldId { get; }
        public int OtherMeldId { get; }
        public int Index { get; }

        internal byte[] CardIdsUnsafe => cardIds ?? Array.Empty<byte>();

        public static GameCommand ChooseDealer(int dealerSeat)
        {
            return new GameCommand(CommandType.ChooseDealer, dealerSeat);
        }

        public static GameCommand ChooseRandomDealer()
        {
            return new GameCommand(CommandType.ChooseRandomDealer, -1);
        }

        public static GameCommand MoveCards(int seat, byte[] cardIds, CardTarget target)
        {
            return new GameCommand(CommandType.MoveCards, seat, cardIds, target, 0, 0, 0);
        }

        public static GameCommand SplitMeld(int seat, int meldId, int index)
        {
            return new GameCommand(CommandType.SplitMeld, seat, null, default, meldId, 0, index);
        }

        public static GameCommand MergeMelds(int seat, int meldId, int otherMeldId)
        {
            return new GameCommand(CommandType.MergeMelds, seat, null, default, meldId, otherMeldId, 0);
        }

        public static GameCommand GoDown(int seat)
        {
            return new GameCommand(CommandType.GoDown, seat);
        }

        public static GameCommand Undo(int seat)
        {
            return new GameCommand(CommandType.Undo, seat);
        }

        public static GameCommand Revert(int seat)
        {
            return new GameCommand(CommandType.Revert, seat);
        }

        public static GameCommand EndTurn(int seat)
        {
            return new GameCommand(CommandType.EndTurn, seat);
        }

        public static GameCommand DrawOrPass(int seat)
        {
            return new GameCommand(CommandType.DrawOrPass, seat);
        }

        public static GameCommand TimeoutTurn(int seat)
        {
            return new GameCommand(CommandType.TimeoutTurn, seat);
        }
    }
}