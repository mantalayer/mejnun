namespace Mejnun.Core
{
    public readonly struct CommandResult
    {
        private CommandResult(bool ok, RuleError error)
        {
            Ok = ok;
            Error = error;
        }

        public bool Ok { get; }
        public RuleError Error { get; }

        public static CommandResult Success()
        {
            return new CommandResult(true, RuleError.None);
        }

        public static CommandResult Failure(RuleError error)
        {
            return new CommandResult(false, error);
        }
    }
}