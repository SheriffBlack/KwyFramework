namespace KwyPecvd.Equipment.Commands;

/// <summary>
/// 定义 Equipment Command 的稳定代码。
/// 这是哪种命令
/// </summary>
public static class EquipmentCommandTypeCodes
{
    public const string StopProcess = "EQUIPMENT_COMMAND_STOP_PROCESS";
}


/// <summary>
/// 定义 Equipment Command 处理过程中产生的稳定结果代码。
/// 命令的处理结果
/// </summary>
public static class EquipmentCommandResultCodes
{
    public const string InvalidArgument = "EQUIPMENT_COMMAND_INVALID_ARGUMENT";

    public const string UnsupportedCommand = "EQUIPMENT_COMMAND_UNSUPPORTED";

    public const string ModuleNotFound = "EQUIPMENT_COMMAND_MODULE_NOT_FOUND";

    public const string Accepted = "EQUIPMENT_COMMAND_ACCEPTED";

    /// <summary>同一个模块已经有相同类型的异步命令尚未完成。 </summary>
    public const string AlreadyPending = "EQUIPMENT_COMMAND_ALREADY_PENDING";

    public const string Completed = "EQUIPMENT_COMMAND_COMPLETED";

    public const string ExecutionFailed = "EQUIPMENT_COMMAND_EXECUTION_FAILED";
}
