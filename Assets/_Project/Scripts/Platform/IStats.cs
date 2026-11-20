namespace Mejnun.Platform
{
    // Integer player stats. Writes may be batched by the backend and flushed
    // on a later Tick or on Shutdown.
    public interface IStats
    {
        int GetInt(StatId id);
        void SetInt(StatId id, int value);
        void IncrementInt(StatId id, int amount);
    }
}
