using KwyTemplate.Vision.Models;

namespace KwyTemplate.Vision.Executors;

/// <summary>
/// 节点执行器基类：提供通用的数据提取、转换及运行保障能力
/// </summary>
public abstract class FlowNodeExecutorBase : IFlowNodeExecutor
{
    /// <summary>对应节点类型（子类实现）</summary>
    public abstract string NodeType { get; }

    /// <summary>核心执行逻辑（子类实现）</summary>
    public abstract Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default);

    /// <summary>
    /// 通用取值逻辑：优先从 inputs (连线) 获取，其次从 Parameters (面板) 获取，最后返回默认值。
    /// 内置了常用的类型转换逻辑，确保计算的稳定性。
    /// </summary>
    /// <typeparam name="T">期望的目标类型</typeparam>
    /// <param name="inputs">上游输入字典</param>
    /// <param name="node">当前节点数据模型</param>
    /// <param name="portName">端口（参数）名称</param>
    /// <param name="defaultValue">缺失或转换失败时的默认值</param>
    protected T GetInputValue<T>(IReadOnlyDictionary<string, FlowValue> inputs, FlowNode node, string portName, T defaultValue = default!)
    {
        object? rawValue = null;

        // 1. 优先：连线传入的数据流
        if (inputs.TryGetValue(portName, out var inputVal) && inputVal.HasValue)
        {
            rawValue = inputVal.Value;
        }
        // 2. 次之：右侧面板填写的固定参数
        else if (node.Parameters.TryGetValue(portName, out var paramVal) && paramVal != null)
        {
            rawValue = paramVal;
        }

        if (rawValue == null) return defaultValue;

        try
        {
            // 3. 处理 System.Text.Json 反序列化产生的 JsonElement
            if (rawValue is System.Text.Json.JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Number:
                        rawValue = element.GetDouble();
                        break;

                    case System.Text.Json.JsonValueKind.True:
                        rawValue = true;
                        break;

                    case System.Text.Json.JsonValueKind.False:
                        rawValue = false;
                        break;

                    case System.Text.Json.JsonValueKind.String:
                        rawValue = element.GetString();
                        break;

                    case System.Text.Json.JsonValueKind.Null:
                        rawValue = null;
                        break;
                }
            }

            if (rawValue == null) return defaultValue;

            // 4. 增强的类型转换逻辑
            Type targetType = typeof(T);

            // 处理可为空类型 (Nullable<T>)
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(double))
                return (T)(object)Convert.ToDouble(rawValue);

            if (underlyingType == typeof(float))
                return (T)(object)Convert.ToSingle(rawValue);

            if (underlyingType == typeof(int))
                return (T)(object)Convert.ToInt32(rawValue);

            if (underlyingType == typeof(bool))
                return (T)(object)Convert.ToBoolean(rawValue);

            if (underlyingType == typeof(string))
                return (T)(object)Convert.ToString(rawValue)!;

            return (T)rawValue;
        }
        catch
        {
            // 转换失败返回默认值，确保整个流程不因单个节点的解析问题而崩溃
            return defaultValue;
        }
    }
}
