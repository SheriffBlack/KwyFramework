namespace Kwy.Device.Core.IO;

/// <summary>
/// 管理可重置的单通道软件定时脉冲。
/// 同一通道再次触发时会取消前一次复位计时并重新计时；受 Windows 调度影响，不能用于实时触发或功能安全。
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

    /// <summary>置位输出并在指定时长后复位。</summary>
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

    /// <summary>取消所有待复位任务；可选择同时将仍可访问的输出复位。</summary>
    public void CancelAll(bool resetOutputs = false)
    {
        List<(int Channel, CancellationTokenSource Token)> tokens;
        lock (syncRoot)
        {
            tokens = pulseTokens.Select(static item => (item.Key, item.Value)).ToList();
            pulseTokens.Clear();
        }

        foreach ((int channel, CancellationTokenSource token) in tokens)
        {
            try
            {
                token.Cancel();
                if (resetOutputs && canResetOutput())
                {
                    writeOutput(channel, false);
                }
                token.Dispose();
            }
            catch (Exception exception)
            {
                onResetError(channel, exception);
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
        CancelAll(resetOutputs: true);
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
