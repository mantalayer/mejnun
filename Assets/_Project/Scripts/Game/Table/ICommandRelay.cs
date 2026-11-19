using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public interface ICommandRelay
    {
        bool IsRemoteAuthority { get; }
        bool ApplyCommandsLocally { get; }
        bool CanControlSeat(int seat);
        bool Submit(IReadOnlyList<GameCommand> commands);
    }
}