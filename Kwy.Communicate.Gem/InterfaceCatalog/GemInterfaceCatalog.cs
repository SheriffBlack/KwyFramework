using System.Collections.ObjectModel;
using System.Collections.Frozen;

namespace Kwy.Communicate.Gem;

/// <summary>提供设备向 Host 公开的、不可变的 GEM 接口合同。</summary>
public interface IGemInterfaceCatalog
{
    IReadOnlyDictionary<uint, GemVariableDefinition> Variables { get; }
    IReadOnlyDictionary<uint, GemEquipmentConstantDefinition> Constants { get; }
    IReadOnlyDictionary<uint, GemReportDefinition> Reports { get; }
    IReadOnlyDictionary<uint, GemCollectionEventDefinition> Events { get; }
    IReadOnlyDictionary<uint, GemAlarmDefinition> Alarms { get; }
    IReadOnlyDictionary<string, GemRemoteCommandDefinition> RemoteCommands { get; }
}

/// <summary>保存设备向 Host 公开的静态、不可变 GEM 接口合同。</summary>
/// <remarks>
/// 请使用 <see cref="GemInterfaceCatalogBuilder"/> 注册和校验接口定义。
/// 该目录定义设备支持的变量、常量、报表、事件、报警和远程命令。
/// </remarks>
public sealed class GemInterfaceCatalog : IGemInterfaceCatalog
{
    internal GemInterfaceCatalog(
        IDictionary<uint, GemVariableDefinition> variables,
        IDictionary<uint, GemEquipmentConstantDefinition> constants,
        IDictionary<uint, GemReportDefinition> reports,
        IDictionary<uint, GemCollectionEventDefinition> events,
        IDictionary<uint, GemAlarmDefinition> alarms,
        IDictionary<string, GemRemoteCommandDefinition> remoteCommands)
    {
        Variables = ToReadOnly(variables.ToDictionary(pair => pair.Key, pair => pair.Value with { }));
        Constants = ToReadOnly(constants.ToDictionary(pair => pair.Key, pair => pair.Value with { }));
        Reports = ToReadOnly(reports.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with { VariableIds = Array.AsReadOnly(pair.Value.VariableIds.ToArray()) }));
        Events = ToReadOnly(events.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with { LinkedReports = Array.AsReadOnly(pair.Value.LinkedReports.ToArray()) }));
        Alarms = ToReadOnly(alarms.ToDictionary(pair => pair.Key, pair => pair.Value with { }));
        RemoteCommands = new ReadOnlyDictionary<string, GemRemoteCommandDefinition>(
            remoteCommands.ToDictionary(
                pair => pair.Key,
                pair => pair.Value with
                {
                    Parameters = Array.AsReadOnly(pair.Value.Parameters.ToArray()),
                    AllowedControlStates = pair.Value.AllowedControlStates?.ToFrozenSet()
                },
                StringComparer.OrdinalIgnoreCase));
    }

    public static GemInterfaceCatalog Empty { get; } = new GemInterfaceCatalogBuilder().Build();

    public IReadOnlyDictionary<uint, GemVariableDefinition> Variables { get; }
    public IReadOnlyDictionary<uint, GemEquipmentConstantDefinition> Constants { get; }
    public IReadOnlyDictionary<uint, GemReportDefinition> Reports { get; }
    public IReadOnlyDictionary<uint, GemCollectionEventDefinition> Events { get; }
    public IReadOnlyDictionary<uint, GemAlarmDefinition> Alarms { get; }
    public IReadOnlyDictionary<string, GemRemoteCommandDefinition> RemoteCommands { get; }

    private static IReadOnlyDictionary<uint, T> ToReadOnly<T>(IDictionary<uint, T> source)
        => new ReadOnlyDictionary<uint, T>(new Dictionary<uint, T>(source));
}

public sealed record GemCatalogValidationIssue(string Code, string Message);

public sealed record GemCatalogValidationResult(IReadOnlyList<GemCatalogValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class GemCatalogValidationException : InvalidOperationException
{
    public GemCatalogValidationException(GemCatalogValidationResult result)
        : base($"The GEM interface catalog is invalid: {string.Join("; ", result.Issues.Select(x => x.Message))}")
    {
        Result = result;
    }

    public GemCatalogValidationResult Result { get; }
}
