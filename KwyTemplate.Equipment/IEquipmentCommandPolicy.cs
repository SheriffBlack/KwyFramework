namespace KwyTemplate.Equipment;

/// <summary>为整机命令提供可组合的准入规则。</summary>
public interface IEquipmentCommandPolicy
{
    EquipmentCommandEvaluation Evaluate(EquipmentCommand command, EquipmentState currentState);
}
