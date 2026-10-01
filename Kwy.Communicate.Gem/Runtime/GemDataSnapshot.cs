using System.Collections.ObjectModel;

namespace Kwy.Communicate.Gem;

/*
Variables   → VID   定义   变量 ID
Constants   → ECID  定义   Equipment Constant ID，设备常量 ID
Alarms      → ALID  定义   Alarm ID，报警 ID
*/

/// <summary>保存当前 GEM 会话需要读取的运行时数据快照。</summary>
/// <remarks>该快照服务于协议报文构造，不应作为设备业务状态的唯一数据源。</remarks>
public sealed class GemDataSnapshot
{
    private readonly object syncRoot = new();
    private readonly Dictionary<uint, GemVariable> variables = new();
    private readonly Dictionary<uint, GemEquipmentConstant> constants = new();
    private readonly Dictionary<uint, GemAlarm> alarms = new();

    public IReadOnlyDictionary<uint, GemVariable> Variables
    {
        get
        {
            lock (syncRoot)
            {
                return new ReadOnlyDictionary<uint, GemVariable>(new Dictionary<uint, GemVariable>(variables));
            }
        }
    }

    public IReadOnlyDictionary<uint, GemEquipmentConstant> Constants
    {
        get
        {
            lock (syncRoot)
            {
                return new ReadOnlyDictionary<uint, GemEquipmentConstant>(new Dictionary<uint, GemEquipmentConstant>(constants));
            }
        }
    }

    public IReadOnlyDictionary<uint, GemAlarm> Alarms
    {
        get
        {
            lock (syncRoot)
            {
                return new ReadOnlyDictionary<uint, GemAlarm>(new Dictionary<uint, GemAlarm>(alarms));
            }
        }
    }

    public void SetVariable(GemVariable variable) => SetVariables(new[] { variable });

    /// <summary>在同一个临界区内更新一组相关变量。</summary>
    public void SetVariables(IEnumerable<GemVariable> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        GemVariable[] snapshot = values.ToArray();
        foreach (GemVariable variable in snapshot)
        {
            ArgumentNullException.ThrowIfNull(variable);
            if (variable.Id == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "VID must be greater than zero.");
            }
        }

        lock (syncRoot)
        {
            foreach (GemVariable variable in snapshot)
            {
                variables[variable.Id] = variable;
            }
        }
    }

    public void SetConstant(GemEquipmentConstant constant)
    {
        ArgumentNullException.ThrowIfNull(constant);
        if (constant.Id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(constant), "ECID must be greater than zero.");
        }

        lock (syncRoot)
        {
            constants[constant.Id] = constant;
        }
    }

    public void SetAlarm(GemAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        if (alarm.AlarmId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alarm), "ALID must be greater than zero.");
        }

        lock (syncRoot)
        {
            alarms[alarm.AlarmId] = alarm;
        }
    }

    public GemVariable GetRequiredVariable(GemVid vid)
    {
        lock (syncRoot)
        {
            return variables.TryGetValue(vid.Value, out GemVariable? variable)
                ? variable
                : throw new InvalidOperationException($"No runtime value is available for VID {vid.Value}.");
        }
    }

    /// <summary>原子读取构造一个事件或 Trace 所需的全部变量。</summary>
    public IReadOnlyDictionary<GemVid, GemVariable> CaptureRequired(IEnumerable<GemVid> variableIds)
    {
        ArgumentNullException.ThrowIfNull(variableIds);
        GemVid[] ids = variableIds.Distinct().ToArray();
        var result = new Dictionary<GemVid, GemVariable>(ids.Length);

        lock (syncRoot)
        {
            foreach (GemVid vid in ids)
            {
                result[vid] = variables.TryGetValue(vid.Value, out GemVariable? variable)
                    ? variable
                    : throw new InvalidOperationException($"No runtime value is available for VID {vid.Value}.");
            }
        }

        return new ReadOnlyDictionary<GemVid, GemVariable>(result);
    }
}
