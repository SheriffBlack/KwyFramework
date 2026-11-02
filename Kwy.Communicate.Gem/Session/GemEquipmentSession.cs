namespace Kwy.Communicate.Gem;

/// <summary>提供设备侧 GEM 会话状态及常用行为的默认编排实现。</summary>
public sealed partial class GemEquipmentSession : IGemEquipmentSession
{
    private readonly ISecsGemClient secsClient;
    private readonly GemRegistry registry;
    private readonly GemTraceService traceService;
    private readonly GemSpoolingService spoolingService;
    private readonly IGemProcessProgramRepository processProgramRepository;
    private readonly bool ownsSpoolingService;
    private readonly SemaphoreSlim controlStateLock = new(1, 1);
    private bool disposed;

    public GemEquipmentSession(
        ISecsGemClient secsClient,
        GemRegistry registry,
        GemTraceService? traceService = null,
        GemSpoolingService? spoolingService = null,
        IGemProcessProgramRepository? processProgramRepository = null,
        GemEndpoint? local = null,
        GemEndpoint? remote = null)
    {
        this.secsClient = secsClient ?? throw new ArgumentNullException(nameof(secsClient));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.traceService = traceService ?? new GemTraceService(registry);
        this.spoolingService = spoolingService ?? new GemSpoolingService();
        ownsSpoolingService = spoolingService is null;
        this.processProgramRepository = processProgramRepository ?? new UnsupportedGemProcessProgramRepository();
        StateMachine = new GemSessionStateMachine();
        LocalEndpoint = local ?? new GemEndpoint(GemHostRole.Equipment, "KwyEquipment", "KWY", "1.0");
        RemoteEndpoint = remote ?? new GemEndpoint(GemHostRole.Host, "Host");
        secsClient.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public GemEndpoint LocalEndpoint { get; }
    public GemEndpoint RemoteEndpoint { get; }
    public GemSessionStateMachine StateMachine { get; }
    public GemCommunicationState CommunicationState => StateMachine.CommunicationState;
    public GemControlState ControlState => StateMachine.ControlState;
    public GemCommunicationContext Context => new(LocalEndpoint, RemoteEndpoint, CommunicationState, ControlState);
    public int SpooledMessageCount => spoolingService.Count;

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        secsClient.ConnectionStateChanged -= OnConnectionStateChanged;
        controlStateLock.Dispose();
        if (ownsSpoolingService)
            spoolingService.Dispose();
    }
}
