using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests.TestModels.UniqueConstraints;

public sealed record YamlUserUpdated(
    string? Email,
    string? Username = null,
    string? Name = null,
    bool IsDeleted = false
) : IEvent
{
    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo)
    {
        var state = (YamlUserStateData)stateData;
        state.Email = Email;
        state.Username = Username;
        state.Name = Name;
        state.IsDeleted = IsDeleted;
        return state;
    }
}
