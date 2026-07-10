using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests;

public class OrderNumberHelperUnitTest
{
    [Fact()]
    public void AssignOrderNumbersToNewEventsIfPreviousExist()
    {
        var payloads = new List<EventPayload>();

        for (uint i = 1; i <= 10; i++)
        {
            var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

            var payload = EventPayload.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "test-state-machine",
                transferMoneyEventData
            );
            payload.EventExecutionInfo.OrderNumber = i;
            payloads.Add(payload);
        }

        var newPayloads = new List<EventPayload>();
        for (int i = 0; i < 3; i++)
        {
            var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

            var payload = EventPayload.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "test-state-machine",
                transferMoneyEventData
            );
            newPayloads.Add(payload);
        }

        var orderNumberHelper = new OrderNumberHelper();
        orderNumberHelper.AssignOrderNumbers(payloads, newPayloads);

        Assert.Equal(newPayloads[0].EventExecutionInfo.OrderNumber.ToString(), 11.ToString());
        Assert.Equal(newPayloads[1].EventExecutionInfo.OrderNumber.ToString(), 12.ToString());
        Assert.Equal(newPayloads[2].EventExecutionInfo.OrderNumber.ToString(), 13.ToString());
    }

    [Fact]
    public void AssignOrderNumbersToNewEventsIfThereAreNoPreviousEvents()
    {
        var payloads = new List<EventPayload>();

        var newPayloads = new List<EventPayload>();
        for (int i = 0; i < 3; i++)
        {
            var transferMoneyEventData = new TransferMoney() { MoneySent = 1000 };

            var payload = EventPayload.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "test-state-machine",
                transferMoneyEventData
            );
            newPayloads.Add(payload);
        }

        var orderNumberHelper = new OrderNumberHelper();
        orderNumberHelper.AssignOrderNumbers(payloads, newPayloads);

        Assert.Equal(newPayloads[0].EventExecutionInfo.OrderNumber.ToString(), 1.ToString());
        Assert.Equal(newPayloads[1].EventExecutionInfo.OrderNumber.ToString(), 2.ToString());
        Assert.Equal(newPayloads[2].EventExecutionInfo.OrderNumber.ToString(), 3.ToString());
    }
}
