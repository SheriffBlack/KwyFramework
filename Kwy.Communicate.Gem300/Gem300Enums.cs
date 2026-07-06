namespace Kwy.Communicate.Gem300;

public enum LoadPortState
{
    Unknown,
    Empty,
    CarrierPresent,
    CarrierClamped,
    CarrierDocked,
    CarrierUndocked,
    Error
}

public enum CarrierAccessState
{
    NotAccessed,
    InAccess,
    CarrierComplete,
    CarrierStopped
}

public enum CarrierTransferState
{
    Unknown,
    TransferBlocked,
    ReadyToLoad,
    Transferring,
    ReadyToUnload,
    TransferComplete
}

public enum CarrierAssociationState
{
    NotAssociated,
    Associated,
    AssociationFailed
}

public enum SlotMapState
{
    Unknown,
    NotRead,
    Reading,
    VerificationOk,
    VerificationFailed
}

public enum SubstrateState
{
    Unknown,
    AtSource,
    InProcess,
    Processed,
    AtDestination,
    Lost,
    Rejected
}

public enum SubstrateLocationType
{
    Unknown,
    Carrier,
    LoadPort,
    Buffer,
    Align,
    ProcessModule,
    Robot,
    Output
}

public enum JobState
{
    Created,
    Queued,
    Selected,
    Executing,
    Paused,
    Completed,
    Aborted,
    Cancelled
}

public enum ProcessJobType
{
    Lot,
    Carrier,
    Substrate
}
