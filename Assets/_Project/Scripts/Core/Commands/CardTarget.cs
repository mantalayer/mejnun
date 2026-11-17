namespace Mejnun.Core
{
    public readonly struct CardTarget
    {
        public CardTarget(TargetKind kind, int meldId, int index)
        {
            Kind = kind;
            MeldId = meldId;
            Index = index;
        }

        public TargetKind Kind { get; }
        public int MeldId { get; }
        public int Index { get; }

        public static CardTarget Hand(int index)
        {
            return new CardTarget(TargetKind.Hand, 0, index);
        }

        public static CardTarget Meld(int meldId, int index)
        {
            return new CardTarget(TargetKind.Meld, meldId, index);
        }

        public static CardTarget NewMeld(int index)
        {
            return new CardTarget(TargetKind.NewMeld, 0, index);
        }
    }
}