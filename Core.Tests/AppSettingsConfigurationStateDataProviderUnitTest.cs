using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class AppSettingsConfigurationStateDataProviderUnitTest
{
    public IConfiguration CreateConfiguration(Dictionary<string, string?> configurationDict)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationDict)
            .Build();

        return configuration;
    }

    [Fact]
    public void StateMachineNotSetInConfigurationTest()
    {
        var myConfiguration = new Dictionary<string, string?>
        {
            { "Key1", "Value1" },
            { "Nested:Key1", "NestedValue1" },
            { "Nested:Key2", "NestedValue2" },
            { "RandomEvent1", null }
        };
        var configuration = CreateConfiguration(myConfiguration);

        var stateDataProvider = new AppSettingsConfigurationStateDataProvider(configuration);

        var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

        var payload = EventPayload.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-state-machine",
            transferMoneyEventData
        );

        try
        {
            stateDataProvider.GetStateDataByStateMachine(payload.EventExecutionInfo.StateMachineId);
        }
        catch (StateMachineNotRegisteredException)
        {
            Assert.True(true);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public void StateDataTypeNotFoundTest()
    {
        var myConfiguration = new Dictionary<string, string?>
        {
            { "Key1", "Value1" },
            { "Nested:Key1", "NestedValue1" },
            { "Nested:Key2", "NestedValue2" },
            { "RandomEvent1", null },
            { "test-state-machine", "AccountTestStateData" }
        };
        var configuration = CreateConfiguration(myConfiguration);

        var stateDataProvider = new AppSettingsConfigurationStateDataProvider(configuration);

        var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

        var payload = EventPayload.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-state-machine",
            transferMoneyEventData
        );

        try
        {
            stateDataProvider.GetStateDataByStateMachine(payload.EventExecutionInfo.StateMachineId);
        }
        catch (StateDataTypeNotFoundException)
        {
            Assert.True(true);
        }
        catch (StateMachineNotRegisteredException)
        {
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }

    [Fact]
    public void StateDataExists()
    {
        var myConfiguration = new Dictionary<string, string?>
        {
            { "Key1", "Value1" },
            { "Nested:Key1", "NestedValue1" },
            { "Nested:Key2", "NestedValue2" },
            { "RandomEvent1", null },
            { "test-state-machine", "AccountStateData" }
        };
        var configuration = CreateConfiguration(myConfiguration);

        var stateDataProvider = new AppSettingsConfigurationStateDataProvider(configuration);

        var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

        var payload = EventPayload.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-state-machine",
            transferMoneyEventData
        );

        try
        {
            stateDataProvider.GetStateDataByStateMachine(payload.EventExecutionInfo.StateMachineId);
            Assert.True(true);
        }
        catch (StateDataNotRegisteredException)
        {
            Assert.True(false);
        }
        catch (StateMachineNotRegisteredException)
        {
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
    }
}
