namespace KwyTemplate.App.Orchestration;

public interface ICyntecReelScanWorkflow
{
    Task ScanAsync(CancellationToken cancellationToken = default);
}