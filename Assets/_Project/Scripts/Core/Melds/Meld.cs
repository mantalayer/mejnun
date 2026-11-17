using System.Collections.Generic;

namespace Mejnun.Core
{
    public sealed class Meld
    {
        public Meld(int id, IEnumerable<Card> cards)
        {
            Id = id;
            Cards = new List<Card>(cards);
            MeldValidator.CanonicalizeStraight(Cards);
        }

        public int Id { get; }
        public List<Card> Cards { get; }
    }
}