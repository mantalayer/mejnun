namespace Mejnun.Core
{
    public readonly struct MeldClassification
    {
        public MeldClassification(MeldKind kind, InvalidReason reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public MeldKind Kind { get; }
        public InvalidReason Reason { get; }
        public bool IsValid => Kind != MeldKind.Invalid;
    }
}