using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Gem;

public sealed class GemSpoolingService
{
    private readonly Queue<GemSpooledMessage> messages = new();
    private long sequence;

    public GemSpoolingService(GemSpoolingOptions? options = null)
    {
        Options = options ?? new GemSpoolingOptions();
    }

    public GemSpoolingOptions Options { get; private set; }

    public GemSpoolingState State { get; private set; } = GemSpoolingState.Disabled;

    public IReadOnlyCollection<GemSpooledMessage> Messages => messages.ToArray();

    public void Configure(GemSpoolingOptions options)
    {
        Options = options;
        State = options.Enabled ? GemSpoolingState.Enabled : GemSpoolingState.Disabled;
        Trim();
    }

    public void Enqueue(SecsMessage message)
    {
        if (!Options.Enabled)
        {
            return;
        }

        State = GemSpoolingState.Active;
        messages.Enqueue(new GemSpooledMessage(++sequence, message, DateTimeOffset.Now));
        Trim();
    }

    public IReadOnlyList<GemSpooledMessage> DequeueBatch(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<GemSpooledMessage>();
        }

        State = GemSpoolingState.Transmitting;
        var result = new List<GemSpooledMessage>(Math.Min(count, messages.Count));
        while (result.Count < count && messages.Count > 0)
        {
            result.Add(messages.Dequeue());
        }

        State = messages.Count == 0 ? GemSpoolingState.Enabled : GemSpoolingState.Active;
        return result;
    }

    public void Purge()
    {
        State = GemSpoolingState.Purging;
        messages.Clear();
        State = Options.Enabled ? GemSpoolingState.Enabled : GemSpoolingState.Disabled;
    }

    private void Trim()
    {
        while (messages.Count > Options.MaximumMessages)
        {
            messages.Dequeue();
        }
    }
}
