using EventSourcing.Persistence.Models;

namespace EventSourcing.Core.Tests;

public sealed class SerializedPayloadMessageTests
{
    [Fact]
    public void FailureReturnsMessageToQueueUntilThirdAttemptThenMarksItAsError()
    {
        var message = new SerializedPayloadMessage();

        message.StartReading();
        message.Fail();
        Assert.Equal(1, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.New, message.Status);

        message.StartReading();
        message.Fail();
        Assert.Equal(2, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.New, message.Status);

        message.StartReading();
        message.Fail();
        Assert.Equal(3, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.Error, message.Status);
    }

    [Fact]
    public void CompletionMarksClaimedMessageAsSentWithoutAddingAnotherAttempt()
    {
        var message = new SerializedPayloadMessage();

        message.StartReading();
        message.Complete();

        Assert.Equal(1, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.Sent, message.Status);
    }
}
