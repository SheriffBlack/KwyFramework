using Kwy.Communicate.Gem;
using Secs4Net;

namespace Kwy.Communicate.Tests;

public sealed class GemSpoolingServiceTests
{
    [Fact]
    public void TryEnqueue_UsesSafeMessageEligibilityDefaults()
    {
        using var spool = new GemSpoolingService(new GemSpoolingOptions(Enabled: true));
        using var collectionEvent = CreateMessage(6, 11, "event");
        using var remoteCommand = CreateMessage(2, 41, "command");
        using var trace = CreateMessage(6, 1, "trace");

        Assert.True(spool.TryEnqueue(collectionEvent));
        Assert.False(spool.TryEnqueue(remoteCommand));
        Assert.False(spool.TryEnqueue(trace));
        Assert.Equal(1, spool.Count);
    }

    [Fact]
    public void TryEnqueue_AmbiguousFailureIsDisabledByDefault()
    {
        using var spool = new GemSpoolingService(new GemSpoolingOptions(Enabled: true));
        using var message = CreateMessage(6, 11, "event");

        bool queued = spool.TryEnqueue(message, ambiguousSendFailure: true);

        Assert.False(queued);
        Assert.Equal(0, spool.Count);
    }

    [Fact]
    public async Task ReplayAsync_RemovesOnlySuccessfullySentMessages()
    {
        using var spool = new GemSpoolingService(new GemSpoolingOptions(Enabled: true));
        using var first = CreateMessage(6, 11, "first");
        using var second = CreateMessage(6, 11, "second");
        spool.Enqueue(first);
        spool.Enqueue(second);
        int calls = 0;

        GemSpoolReplayResult result = await spool.ReplayAsync((message, _) =>
        {
            calls++;
            if (calls == 2)
                throw new IOException("simulated failure");
            return Task.CompletedTask;
        });

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Attempted);
        Assert.Equal(1, result.Sent);
        Assert.Equal(1, result.Remaining);
        Assert.Equal(1, spool.Count);
        Assert.Equal("second", spool.Messages.Single().Message.SecsItem!.GetString());
    }

    [Fact]
    public void TrimAndPurge_KeepBoundedQueueConsistent()
    {
        using var spool = new GemSpoolingService(new GemSpoolingOptions(
            Enabled: true,
            MaximumMessages: 2,
            OverflowPolicy: GemSpoolOverflowPolicy.DropOldest));
        using var first = CreateMessage(6, 11, "first");
        using var second = CreateMessage(6, 11, "second");
        using var third = CreateMessage(6, 11, "third");
        spool.Enqueue(first);
        spool.Enqueue(second);
        spool.Enqueue(third);

        Assert.Equal(2, spool.Count);
        Assert.Equal("second", spool.Messages.First().Message.SecsItem!.GetString());

        spool.Purge();
        Assert.Equal(0, spool.Count);
        Assert.Equal(GemSpoolingState.Enabled, spool.State);
    }

    [Fact]
    public void Overflow_RejectsNewestByDefault()
    {
        using var spool = new GemSpoolingService(new GemSpoolingOptions(
            Enabled: true,
            MaximumMessages: 1));
        using var first = CreateMessage(6, 11, "first");
        using var second = CreateMessage(6, 11, "second");

        Assert.True(spool.TryEnqueue(first));
        Assert.False(spool.TryEnqueue(second));
        Assert.Equal("first", spool.Messages.Single().Message.SecsItem!.GetString());
    }

    private static SecsMessage CreateMessage(byte stream, byte function, string value)
        => new(stream, function, replyExpected: true)
        {
            SecsItem = Item.A(value)
        };
}
