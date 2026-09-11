using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;

namespace KwyTemplate.Vision.Executors;

public sealed class MathNumberConstantExecutor : FlowNodeExecutorBase
{
    public override string NodeType => FlowNodeTypes.MathNumberConstant;

    public override Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        double value = GetInputValue<double>(inputs, node, FlowParameterKeys.Value);

        return Task.FromResult(FlowNodeExecutionResult.OkObjects(new Dictionary<string, object?>
        {
            [FlowPortNames.Output] = value
        }));
    }
}

public sealed class MathAddExecutor : FlowNodeExecutorBase
{
    public override string NodeType => FlowNodeTypes.MathAdd;

    public override Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        double valueA = GetInputValue<double>(inputs, node, FlowParameterKeys.ValueA);
        double valueB = GetInputValue<double>(inputs, node, FlowParameterKeys.ValueB);

        return Task.FromResult(FlowNodeExecutionResult.OkObjects(new Dictionary<string, object?>
        {
            [FlowPortNames.Result] = valueA + valueB
        }));
    }
}

public sealed class LogicRangeJudgementExecutor : FlowNodeExecutorBase
{
    public override string NodeType => FlowNodeTypes.LogicRangeJudgement;

    public override Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        double value = GetInputValue<double>(inputs, node, FlowParameterKeys.Value);
        double min = GetInputValue<double>(inputs, node, FlowParameterKeys.Minimum, 0.0);
        double max = GetInputValue<double>(inputs, node, FlowParameterKeys.Maximum, 100.0);

        return Task.FromResult(FlowNodeExecutionResult.OkObjects(new Dictionary<string, object?>
        {
            [FlowPortNames.Ok] = value >= min && value <= max
        }));
    }
}

public sealed class LogicDelayExecutor : FlowNodeExecutorBase
{
    public override string NodeType => FlowNodeTypes.LogicDelay;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        int delayMs = Math.Max(0, GetInputValue<int>(inputs, node, FlowParameterKeys.DelayMs, 500));
        await Task.Delay(delayMs, ct).ConfigureAwait(false);

        inputs.TryGetValue(FlowPortNames.Input, out var input);
        return FlowNodeExecutionResult.Ok(new Dictionary<string, FlowValue>
        {
            [FlowPortNames.Output] = input is { HasValue: true } ? input : FlowValue.From(true, PortDataTypes.Boolean)
        });
    }
}
