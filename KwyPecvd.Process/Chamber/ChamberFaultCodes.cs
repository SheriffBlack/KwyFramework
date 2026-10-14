namespace KwyPecvd.Process.Chamber;

/// <summary>腔体状态机对外发布的稳定故障码。</summary>
public static class ChamberFaultCodes
{
    public const string InitializeFailed = "CHAMBER_INITIALIZE_FAILED";

    public const string GasMfcNotAvailable = "CHAMBER_GAS_MFC_NOT_AVAILABLE";

    public const string GasMfcOffline = "CHAMBER_GAS_MFC_OFFLINE";

    public const string GasFeedbackInvalid = "CHAMBER_GAS_FEEDBACK_INVALID";

    public const string GasOutOfTolerance = "CHAMBER_GAS_OUT_OF_TOLERANCE";

    public const string GasPreparationFailed = "CHAMBER_GAS_PREPARATION_FAILED";

    public const string GasPreparationCancelFailed = "CHAMBER_GAS_PREPARATION_CANCEL_FAILED";

    public const string ProcessStopFailed = "CHAMBER_PROCESS_STOP_FAILED";

    public const string ProcessStopTimeout = "CHAMBER_PROCESS_STOP_TIMEOUT";

    public const string GasStopTimeout = "CHAMBER_GAS_STOP_TIMEOUT";
}