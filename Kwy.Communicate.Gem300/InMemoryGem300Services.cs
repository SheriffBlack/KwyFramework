using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem300;

public sealed class InMemoryCarrierManager : ICarrierManager
{
    private readonly ConcurrentDictionary<string, Carrier> carriers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Carrier> Carriers => carriers.Values.ToArray();

    public Task RegisterCarrierAsync(Carrier carrier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        cancellationToken.ThrowIfCancellationRequested();
        carriers[carrier.CarrierId] = carrier;
        return Task.CompletedTask;
    }

    public Task UpdateCarrierStateAsync(string carrierId, CarrierAccessState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierId);
        cancellationToken.ThrowIfCancellationRequested();
        carriers.AddOrUpdate(carrierId, id => throw new KeyNotFoundException($"Carrier {id} is not registered."), (_, carrier) => carrier with { AccessState = state });
        return Task.CompletedTask;
    }
}

public sealed class InMemoryLoadPortManager : ILoadPortManager
{
    private readonly ConcurrentDictionary<int, LoadPort> loadPorts = new();

    public IReadOnlyCollection<LoadPort> LoadPorts => loadPorts.Values.ToArray();

    public Task UpdateLoadPortAsync(LoadPort loadPort, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        loadPorts[loadPort.LoadPortId] = loadPort;
        return Task.CompletedTask;
    }
}

public sealed class InMemorySubstrateTracker : ISubstrateTracker
{
    private readonly ConcurrentDictionary<string, Substrate> substrates = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Substrate> Substrates => substrates.Values.ToArray();

    public Task UpdateSubstrateAsync(Substrate substrate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(substrate);
        cancellationToken.ThrowIfCancellationRequested();
        substrates[substrate.SubstrateId] = substrate;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryProcessJobManager : IProcessJobManager
{
    private readonly ConcurrentDictionary<string, ProcessJob> jobs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ProcessJob> ProcessJobs => jobs.Values.ToArray();

    public Task SaveAsync(ProcessJob processJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processJob);
        cancellationToken.ThrowIfCancellationRequested();
        jobs[processJob.ProcessJobId] = processJob;
        return Task.CompletedTask;
    }

    public Task UpdateStateAsync(string processJobId, JobState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processJobId);
        cancellationToken.ThrowIfCancellationRequested();
        jobs.AddOrUpdate(processJobId, id => throw new KeyNotFoundException($"Process job {id} is not registered."), (_, job) => job with { State = state });
        return Task.CompletedTask;
    }
}

public sealed class InMemoryControlJobManager : IControlJobManager
{
    private readonly ConcurrentDictionary<string, ControlJob> jobs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ControlJob> ControlJobs => jobs.Values.ToArray();

    public Task SaveAsync(ControlJob controlJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(controlJob);
        cancellationToken.ThrowIfCancellationRequested();
        jobs[controlJob.ControlJobId] = controlJob;
        return Task.CompletedTask;
    }

    public Task UpdateStateAsync(string controlJobId, JobState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlJobId);
        cancellationToken.ThrowIfCancellationRequested();
        jobs.AddOrUpdate(controlJobId, id => throw new KeyNotFoundException($"Control job {id} is not registered."), (_, job) => job with { State = state });
        return Task.CompletedTask;
    }
}

public sealed class InMemoryGem300History : IGem300History
{
    private readonly ConcurrentQueue<Gem300ObjectEvent> events = new();

    public IReadOnlyCollection<Gem300ObjectEvent> Events => events.ToArray();

    public Task RecordAsync(Gem300ObjectEvent objectEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Enqueue(objectEvent);
        return Task.CompletedTask;
    }
}
