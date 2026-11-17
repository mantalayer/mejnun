namespace Mejnun.Game
{
    public interface IFeedbackSink
    {
        void Invalid();
    }

    public sealed class NoOpFeedbackSink : IFeedbackSink
    {
        public void Invalid()
        {
        }
    }
}