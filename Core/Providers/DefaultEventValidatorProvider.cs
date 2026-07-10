using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.Configuration;
using Shared.Interfaces;

namespace EventSourcing.Core.Providers
{
    public class DefaultEventValidatorProvider : IEventValidatorProvider
    {
        private readonly IConfiguration _configuration;

        public DefaultEventValidatorProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<List<IPostEventValidator>> GetPostEventStateValidators(EventPayload payload)
        {
            return Task.FromResult(new List<IPostEventValidator>());
        }

        public Task<List<IPreEventValidator>> GetPreEventStateValidators(EventPayload payload)
        {
            return Task.FromResult(new List<IPreEventValidator>());
        }
    }
}
