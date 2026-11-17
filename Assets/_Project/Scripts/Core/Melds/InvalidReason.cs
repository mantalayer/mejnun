namespace Mejnun.Core
{
    public enum InvalidReason : byte
    {
        None,
        TooShort,
        TooLong,
        Double,
        MixedSuits,
        MixedRanks,
        NotConsecutive,
        WrapAround
    }
}