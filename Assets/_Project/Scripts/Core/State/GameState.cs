using System.Collections.Generic;

namespace Mejnun.Core
{
    public sealed class GameState
    {
        internal GameState(int seatCount, Pcg32 rng)
        {
            Phase = GamePhase.ChoosingDealer;
            CurrentSeat = -1;
            NextMeldId = 1;
            Rng = rng;
            Board = new List<Meld>();
            DrawPile = new List<Card>();
            Hands = new List<List<Card>>(seatCount);
            PlayerPhases = new List<PlayerPhase>(seatCount);
            DeckFaces = new string[Card.DeckCount];
            TableFeltId = TableFeltAssigner.GreenFelt;

            for (var seat = 0; seat < seatCount; seat++)
            {
                Hands.Add(new List<Card>(11));
                PlayerPhases.Add(PlayerPhase.NotDown);
            }
        }

        public GamePhase Phase { get; internal set; }
        public List<List<Card>> Hands { get; }
        public List<PlayerPhase> PlayerPhases { get; }
        public List<Meld> Board { get; }
        public List<Card> DrawPile { get; }
        public int CurrentSeat { get; internal set; }
        public int? Dealer { get; internal set; }
        public int TurnNumber { get; internal set; }
        public int ConsecutivePasses { get; internal set; }
        public int NextMeldId { get; internal set; }
        public Pcg32 Rng { get; internal set; }
        public string[] DeckFaces { get; internal set; }
        public string TableFeltId { get; internal set; }
        public GameResult Result { get; internal set; }
    }
}