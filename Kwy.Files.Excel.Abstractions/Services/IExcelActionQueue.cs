namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Serializes Excel automation operations. Interop providers can use an STA implementation.
/// </summary>
public interface IExcelActionQueue : IAsyncDisposable
{
    Task RunAsync(Action action, CancellationToken cancellationToken = default);

    Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
}
