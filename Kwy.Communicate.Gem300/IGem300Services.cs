namespace Kwy.Communicate.Gem300;

public interface ICarrierManager
{
    IReadOnlyCollection<Carrier> Carriers { get; }

    Task RegisterCarrierAsync(Carrier carrier, CancellationToken cancellationToken = default);

    Task UpdateCarrierStateAsync(string carrierId, CarrierAccessState state, CancellationToken cancellationToken = default);
}

public interface ILoadPortManager
{
    IReadOnlyCollection<LoadPort> LoadPorts { get; }

    Task UpdateLoadPortAsync(LoadPort loadPort, CancellationToken cancellationToken = default);
}

public interface ISubstrateTracker
{
    IReadOnlyCollection<Substrate> Substrates { get; }

    Task UpdateSubstrateAsync(Substrate substrate, CancellationToken cancellationToken = default);
}

public interface IProcessJobManager
{
    IReadOnlyCollection<ProcessJob> ProcessJobs { get; }

    Task SaveAsync(ProcessJob processJob, CancellationToken cancellationToken = default);

    Task UpdateStateAsync(string processJobId, JobState state, CancellationToken cancellationToken = default);
}

public interface IControlJobManager
{
    IReadOnlyCollection<ControlJob> ControlJobs { get; }

    Task SaveAsync(ControlJob controlJob, CancellationToken cancellationToken = default);

    Task UpdateStateAsync(string controlJobId, JobState state, CancellationToken cancellationToken = default);
}

public interface IGem300History
{
    IReadOnlyCollection<Gem300ObjectEvent> Events { get; }

    Task RecordAsync(Gem300ObjectEvent objectEvent, CancellationToken cancellationToken = default);
}
