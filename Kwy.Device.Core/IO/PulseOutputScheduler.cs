namespace Kwy.Device.Core.IO;

/// <summary>
/// Schedules resettable single-channel pulse output operations.
/// </summary>
public sealed class PulseOutputScheduler : IDisposable
{
    private readonly object syncRoot = new();
    private readonly Dictionary<int, CancellationTokenSource> pulseTokens = new();
    private readonly Action<int, bool> writeOutput;
    private readonly Func<bool> canResetOutput;
    private readonly Action<int, Exception> onResetError;
    private bool disposed;

    public PulseOutputScheduler(
        Action<int, bool> writeOutput,
        Func<bool> canResetOutput,
        Action<int, Exception> onResetError)
    {
        this.writeOutput = writeOutput ?? throw new ArgumentNullException(nameof(writeOutput));
        this.canResetOutput = canResetOutput ?? throw new ArgumentNullException(nameof(canResetOutput));
        this.onResetError = onResetError ?? throw new ArgumentNullException(nameof(onResetError));
    }

    public void WritePulse(int channel, int durationMs)
    {
        if (durationMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Pulse duration cannot be negative.");
        }

        CancellationTokenSource? oldToken = null;
        CancellationTokenSource newToken;
        lock (syncRoot)
        {
            ThrowIfDisposed();
            if (pulseTokens.Remove(channel, out oldToken))
            {
                oldToken.Cancel();
            }

            newToken = new CancellationTokenSource();
            pulseTokens[channel] = newToken;
            writeOutput(channel, true);
        }

        oldToken?.Dispose();
        _ = ResetPulseAsync(channel, durationMs, newToken);
    }

    public void CancelAll()
    {
        List<CancellationTokenSource> tokens;
        lock (syncRoot)
        {
            tokens = pulseTokens.Values.ToList();
            pulseTokens.Clear();
        }

        foreach (CancellationTokenSource token in tokens)
        {
            try
            {
                token.Cancel();
                token.Dispose();
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelAll();
    }

    private async Task ResetPulseAsync(int channel, int durationMs, CancellationTokenSource pulseToken)
    {
        try
        {
            await Task.Delay(durationMs, pulseToken.Token).ConfigureAwait(false);
            lock (syncRoot)
            {
                if (!disposed && canResetOutput())
                {
                    writeOutput(channel, false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            onResetError(channel, ex);
        }
        finally
        {
            var shouldDispose = false;
            lock (syncRoot)
            {
                if (pulseTokens.TryGetValue(channel, out CancellationTokenSource? currentToken)
                    && ReferenceEquals(currentToken, pulseToken))
                {
                    pulseTokens.Remove(channel);
                    shouldDispose = true;
                }
            }

            if (shouldDispose)
            {
                pulseToken.Dispose();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(PulseOutputScheduler));
        }
    }
}
