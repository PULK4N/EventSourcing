using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IProjector
{
    Task Update(List<StateInfo> stateInfo);
}
