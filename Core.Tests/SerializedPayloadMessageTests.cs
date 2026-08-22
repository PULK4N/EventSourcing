using EventSourcing.Persistence.Models;

namespace EventSourcing.Core.Tests;

public sealed class SerializedPayloadMessageTests
{
    [Fact]
    public void FailureReturnsMessageToQueueUntilThirdAttemptThenMarksItAsError()
    {
        var message = new SerializedPayloadMessage();

        message.StartReading();
        message.Fail("First failure");
        Assert.Equal(1, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.New, message.Status);
        Assert.Equal("First failure", message.Error);

        message.StartReading();
        message.Fail("Second failure");
        Assert.Equal(2, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.New, message.Status);
        Assert.Equal("Second failure", message.Error);

        message.StartReading();
        message.Fail("Final failure");
        Assert.Equal(3, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.Error, message.Status);
        Assert.Equal("Final failure", message.Error);
    }

    [Fact]
    public void CompletionMarksClaimedMessageAsSentWithoutAddingAnotherAttempt()
    {
        var message = new SerializedPayloadMessage { Error = "Previous failure" };

        message.StartReading();
        message.Complete();

        Assert.Equal(1, message.ExecutionAttempts);
        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.Null(message.Error);
    }
}
