using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Exceptions;
using Microsoft.Extensions.Configuration;

namespace EventSourcing.Core.Providers
{
    public class AppSettingsConfigurationStateDataProvider : IStateDataProvider
    {
        private readonly IConfiguration _configuration;

        public AppSettingsConfigurationStateDataProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<object> GetStateDataByStateMachine(string stateMachineId)
        {
            var stateDataName = _configuration[stateMachineId];
            if (stateDataName is null)
                throw new StateMachineNotRegisteredException(stateMachineId);
            var type = StateDataTypeContainer.GetStateDataType(stateDataName);
            if (type is null)
                throw new StateDataTypeNotFoundException(stateDataName);

            var stateData = Activator.CreateInstance(type);
            if (stateData is null)
                throw new StateDataNotRegisteredException(stateDataName);

            return Task.FromResult(stateData);
        }
    }
}
