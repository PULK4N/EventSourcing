using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core.Interfaces
{
    public interface IEventValidatorProvider
    {
        Task<List<IPreEventValidator>> GetPreEventStateValidators(EventPayload payload);
        Task<List<IPostEventValidator>> GetPostEventStateValidators(EventPayload payload);
    }
}
