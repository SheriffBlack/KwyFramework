namespace KwyTemplate.Equipment;

/// <summary>保证生产命令与当前整机状态匹配。</summary>
internal sealed class EquipmentStateCommandPolicy : IEquipmentCommandPolicy
{
    public EquipmentCommandEvaluation Evaluate(EquipmentCommand command, EquipmentState currentState)
        => command switch
        {
            EquipmentCommand.StartProduction when currentState == EquipmentState.Stopped
                => EquipmentCommandEvaluation.Allowed,
            EquipmentCommand.StopProduction when currentState == EquipmentState.Running
                => EquipmentCommandEvaluation.Allowed,
            EquipmentCommand.StartProduction
                => EquipmentCommandEvaluation.Denied(
                    "Equipment.NotStopped",
                    "整机只有在停止状态下才能启动生产。"),
            EquipmentCommand.StopProduction
                => EquipmentCommandEvaluation.Denied(
                    "Equipment.NotRunning",
                    "整机当前未在生产运行。"),
            _ => EquipmentCommandEvaluation.Denied(
                "Equipment.UnsupportedCommand",
                $"不支持的整机命令：{command}。")
        };
}
