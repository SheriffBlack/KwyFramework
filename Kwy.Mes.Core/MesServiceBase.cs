using Kwy.Mes.Abstractions;
using Kwy.Mes.Abstractions.Enums;
using Kwy.Mes.Abstractions.Events;
using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Core;

/// <summary>
/// Base class for MES services. It owns lifecycle state but does not prescribe transport.
/// </summary>
public abstract class MesServiceBase : IMesService
{
    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);
    private MesOnlineState state = MesOnlineState.Offline;

    public MesOnlineState State
    {
        get => state;
        protected set
        {
            if (state == value)
            {
                return;
            }

            var oldState = state;
            state = value;
            StateChanged?.Invoke(this, new MesStateChangedEventArgs(oldState, value));
        }
    }

    public bool IsOnline => State == MesOnlineState.Online;

    public event EventHandler<MesStateChangedEventArgs>? StateChanged;

    public async Task<MesResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOnline)
            {
                return MesResult.Ok();
            }

            State = MesOnlineState.Connecting;
            var result = await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
            State = result.Succeeded ? MesOnlineState.Online : MesOnlineState.Error;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = MesOnlineState.Error;
            return MesResult.Fail("CONNECT_FAILED", ex.Message, ex.ToString());
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async Task<MesResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
            State = MesOnlineState.Offline;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = MesOnlineState.Error;
            return MesResult.Fail("DISCONNECT_FAILED", ex.Message, ex.ToString());
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public virtual Task<MesResult<MesWorkOrder>> GetWorkOrderAsync(string workOrderNo, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult<MesWorkOrder>.Unsupported(nameof(GetWorkOrderAsync)));

    public virtual Task<MesResult<MesRouteCheckResult>> CheckRouteAsync(MesUnit unit, MesStation station, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult<MesRouteCheckResult>.Unsupported(nameof(CheckRouteAsync)));

    public virtual Task<MesResult<MesRecipe>> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult<MesRecipe>.Unsupported(nameof(GetRecipeAsync)));

    public virtual Task<MesResult> UploadTestResultAsync(MesTestResult result, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult.Unsupported(nameof(UploadTestResultAsync)));

    public virtual Task<MesResult> UploadTraceAsync(MesTraceRecord record, CancellationToken cancellationToken = default)
        => Task.FromResult(MesResult.Unsupported(nameof(UploadTraceAsync)));

    protected virtual Task<MesResult> ConnectCoreAsync(CancellationToken cancellationToken)
        => Task.FromResult(MesResult.Ok());

    protected virtual Task<MesResult> DisconnectCoreAsync(CancellationToken cancellationToken)
        => Task.FromResult(MesResult.Ok());
}
