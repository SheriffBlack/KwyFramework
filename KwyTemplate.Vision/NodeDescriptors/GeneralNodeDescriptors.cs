using Kwy.ComponentModel;
using KwyTemplate.Vision.Models;

namespace KwyTemplate.Vision.NodeDescriptors;

public sealed class MathNumberConstantDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.MathNumberConstant;
    public string DisplayName => "数值常量";
    public string Category => "数学运算";
    public string Description => "输出一个固定数值。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(
            FlowParameterKeys.Value,
            displayName: "数值",
            defaultValue: 0.0,
            description: "常量输出值。",
            smallChange: 0.1,
            decimalPlaces: 3)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        OutputPorts = [new FlowPort { Name = FlowPortNames.Output, Direction = PortDirection.Output, DataType = PortDataTypes.Number }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class MathAddDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.MathAdd;
    public string DisplayName => "数值相加";
    public string Category => "数学运算";
    public string Description => "计算两个数值的和。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(FlowParameterKeys.ValueA, displayName: "数值 A", defaultValue: 0.0, smallChange: 0.1, decimalPlaces: 3),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.ValueB, displayName: "数值 B", defaultValue: 0.0, smallChange: 0.1, decimalPlaces: 3)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts =
        [
            new FlowPort { Name = FlowParameterKeys.ValueA, Direction = PortDirection.Input, DataType = PortDataTypes.Number },
            new FlowPort { Name = FlowParameterKeys.ValueB, Direction = PortDirection.Input, DataType = PortDataTypes.Number }
        ],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Result, Direction = PortDirection.Output, DataType = PortDataTypes.Number }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class LogicRangeJudgementDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.LogicRangeJudgement;
    public string DisplayName => "范围判定";
    public string Category => "逻辑控制";
    public string Description => "根据上下限输出 OK/NG 判定结果。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(FlowParameterKeys.Minimum, displayName: "最小值", defaultValue: 0.0, smallChange: 0.1, decimalPlaces: 3),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.Maximum, displayName: "最大值", defaultValue: 100.0, smallChange: 0.1, decimalPlaces: 3)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowParameterKeys.Value, Direction = PortDirection.Input, DataType = PortDataTypes.Number }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Ok, Direction = PortDirection.Output, DataType = PortDataTypes.Boolean }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class LogicDelayDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.LogicDelay;
    public string DisplayName => "延时等待";
    public string Category => "逻辑控制";
    public string Description => "等待指定毫秒后继续向下游传递数据。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<int>(FlowParameterKeys.DelayMs, displayName: "延时(ms)", defaultValue: 500, minimum: 0, smallChange: 100, decimalPlaces: 0)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.Input, Direction = PortDirection.Input, DataType = PortDataTypes.Any }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Output, Direction = PortDirection.Output, DataType = PortDataTypes.Any }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class IoReadDigitalInputDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.IoReadDigitalInput;
    public string DisplayName => "读取 DI";
    public string Category => "通信";
    public string Description => "读取一路数字输入信号。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(FlowParameterKeys.DeviceName, displayName: "设备名", defaultValue: string.Empty),
        KwyParameterDefinition.Create<int>(FlowParameterKeys.Channel, displayName: "通道", defaultValue: 0, minimum: 0, smallChange: 1, decimalPlaces: 0)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        OutputPorts = [new FlowPort { Name = FlowPortNames.Signal, Direction = PortDirection.Output, DataType = PortDataTypes.Boolean }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class IoWriteDigitalOutputDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.IoWriteDigitalOutput;
    public string DisplayName => "写入 DO";
    public string Category => "通信";
    public string Description => "写入一路数字输出信号。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(FlowParameterKeys.DeviceName, displayName: "设备名", defaultValue: string.Empty),
        KwyParameterDefinition.Create<int>(FlowParameterKeys.Channel, displayName: "通道", defaultValue: 0, minimum: 0, smallChange: 1, decimalPlaces: 0)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.Signal, Direction = PortDirection.Input, DataType = PortDataTypes.Boolean }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}
