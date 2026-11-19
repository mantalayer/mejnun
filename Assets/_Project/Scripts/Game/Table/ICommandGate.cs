using Mejnun.Core;

namespace Mejnun.Game
{
    /// <summary>Where a command sent through <see cref="TableController"/> came from.</summary>
    public enum CommandOrigin
    {
        /// <summary>A drag, tap or other table gesture.</summary>
        Gesture,

        /// <summary>A direct command: HUD buttons (End turn, Draw, Revert) and bot moves.</summary>
        Command,
        Undo,
        Redo,
        FixBoard
    }

    /// <summary>
    /// Optional filter checked before any command reaches the engine. A rejected command leaves the game untouched
    /// and is reported through <see cref="TableController.InvalidGesture"/> as <see cref="RuleError.TutorialBlocked"/>.
    /// </summary>
    public interface ICommandGate
    {
        bool Allows(in GameCommand command, CommandOrigin origin, GameState state);
    }
}
