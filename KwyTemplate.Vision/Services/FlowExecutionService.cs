using KwyTemplate.Vision.Executors;
using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.Registries;
using KwyTemplate.Vision.ViewModels.Items;
using Kwy.Vision.Abstractions.Images;
using System.Diagnostics;

namespace KwyTemplate.Vision.Services;

/// <summary>
/// Flow execution engine: scheduling, cancellation, breakpoints, single step and runtime result aggregation.
/// </summary>
public sealed class FlowExecutionService
{
    private readonly FlowNodeExecutorRegistry executorRegistry;
    private readonly object gate = new();
    private CancellationTokenSource? runCts;
    private TaskCompletionSource? pauseCompletion;
    private bool stepRequested;

    public FlowExecutionService(FlowNodeExecutorRegistry executorRegistry)
    {
        this.executorRegistry = executorRegistry;
    }

    public bool IsRunning { get; private set; }

    public bool IsPaused { get; private set; }

    public FlowNodeViewModel? PausedNode { get; private set; }

    public void Stop()
    {
        lock (gate)
        {
            runCts?.Cancel();
            pauseCompletion?.TrySetResult();
            pauseCompletion = null;
            IsPaused = false;
            PausedNode = null;
        }
    }

    public void Continue()
    {
        lock (gate)
        {
            stepRequested = false;
            pauseCompletion?.TrySetResult();
            pauseCompletion = null;
            IsPaused = false;
            PausedNode = null;
        }
    }

    public void Step()
    {
        lock (gate)
        {
            stepRequested = true;
            pauseCompletion?.TrySetResult();
            pauseCompletion = null;
            IsPaused = false;
            PausedNode = null;
        }
    }

    public async Task<FlowExecutionResult> ExecuteFlowAsync(
        IEnumerable<FlowNodeViewModel> nodes,
        IEnumerable<FlowConnectionViewModel> connections,
        bool isDebug,
        FlowGraph graph,
        Action<FlowRuntimeEvent>? runtimeEventSink = null,
        Guid? stopAfterNodeId = null,
        IReadOnlyDictionary<string, object?>? contextItems = null,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("流程正在运行中，请先停止当前执行。");
            }

            IsRunning = true;
            stepRequested = isDebug;
            runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        int executedCount = 0;
        var nodeList = nodes.ToList();
        var connectionList = connections.ToList();
        var ct = runCts.Token;
        var context = new FlowExecutionContext(graph, isDebug, ct);
        if (contextItems != null)
        {
            foreach (var item in contextItems)
            {
                context.Items[item.Key] = item.Value;
            }
        }
        var nodeRecords = new List<FlowNodeRunRecord>(nodeList.Count);

        try
        {
            ResetRuntimeState(nodeList, connectionList);
            runtimeEventSink?.Invoke(new FlowRuntimeEvent
            {
                Kind = FlowRuntimeEventKind.FlowStarted,
                Message = $"流程开始：节点={nodeList.Count}，连线={connectionList.Count}"
            });

            var pendingNodes = nodeList.Where(n => !n.IsDisabled).ToList();
            int maxIterations = Math.Max(1, pendingNodes.Count * pendingNodes.Count);
            int safetyCount = 0;

            while (pendingNodes.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (++safetyCount > maxIterations)
                {
                    return FlowExecutionResult.Failed(
                        pendingNodes.FirstOrDefault(),
                        "流程调度超过安全迭代次数，请检查是否存在环路或缺失输入。",
                        executedCount,
                        stopwatch.Elapsed,
                        nodeRecords,
                        context.Variables,
                        context.Images);
                }

                var readyNodes = pendingNodes
                    .Where(node => IsNodeReady(node, connectionList))
                    .ToList();

                if (readyNodes.Count == 0)
                {
                    return FlowExecutionResult.Failed(
                        pendingNodes.FirstOrDefault(),
                        "没有可继续执行的节点，请检查输入连线、断开的端口或循环依赖。",
                        executedCount,
                        stopwatch.Elapsed,
                        nodeRecords,
                        context.Variables,
                        context.Images);
                }

                foreach (var node in readyNodes)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitForDebuggerAsync(node, connectionList, isDebug, runtimeEventSink, ct).ConfigureAwait(false);

                    runtimeEventSink?.Invoke(new FlowRuntimeEvent
                    {
                        Kind = FlowRuntimeEventKind.NodeStarted,
                        Node = node,
                        Message = $"节点开始：{node.DisplayName}"
                    });

                    var nodeStopwatch = Stopwatch.StartNew();
                    var nodeResult = await ExecuteNodeInternalAsync(node, connectionList, graph, context, ct).ConfigureAwait(false);
                    nodeStopwatch.Stop();
                    node.LastElapsed = nodeStopwatch.Elapsed;
                    executedCount++;

                    if (!nodeResult.Success)
                    {
                        node.RuntimeMessage = nodeResult.ErrorMessage;
                        nodeRecords.Add(CreateNodeRecord(node, false, nodeStopwatch.Elapsed, nodeResult.ErrorMessage));
                        runtimeEventSink?.Invoke(new FlowRuntimeEvent
                        {
                            Kind = FlowRuntimeEventKind.NodeFailed,
                            Node = node,
                            Elapsed = nodeStopwatch.Elapsed,
                            Message = nodeResult.ErrorMessage ?? $"节点失败：{node.DisplayName}"
                        });

                        return FlowExecutionResult.Failed(
                            node,
                            nodeResult.ErrorMessage ?? $"节点 {node.DisplayName} 执行失败。",
                            executedCount,
                            stopwatch.Elapsed,
                            nodeRecords,
                            context.Variables,
                            context.Images);
                    }

                    node.RuntimeMessage = null;
                    nodeRecords.Add(CreateNodeRecord(node, true, nodeStopwatch.Elapsed, null));
                    runtimeEventSink?.Invoke(new FlowRuntimeEvent
                    {
                        Kind = FlowRuntimeEventKind.NodeCompleted,
                        Node = node,
                        Elapsed = nodeStopwatch.Elapsed,
                        Message = $"节点完成：{node.DisplayName}，{nodeStopwatch.Elapsed.TotalMilliseconds:F0} ms"
                    });

                    pendingNodes.Remove(node);

                    if (stopAfterNodeId == node.NodeId)
                    {
                        runtimeEventSink?.Invoke(new FlowRuntimeEvent
                        {
                            Kind = FlowRuntimeEventKind.FlowCompleted,
                            Elapsed = stopwatch.Elapsed,
                            Message = $"运行到节点完成：{node.DisplayName}，{stopwatch.Elapsed.TotalMilliseconds:F0} ms"
                        });

                        return FlowExecutionResult.Completed(executedCount, stopwatch.Elapsed, nodeRecords, context.Variables, context.Images);
                    }
                }
            }

            runtimeEventSink?.Invoke(new FlowRuntimeEvent
            {
                Kind = FlowRuntimeEventKind.FlowCompleted,
                Elapsed = stopwatch.Elapsed,
                Message = $"流程完成：{executedCount} 个节点，{stopwatch.Elapsed.TotalMilliseconds:F0} ms"
            });

            return FlowExecutionResult.Completed(executedCount, stopwatch.Elapsed, nodeRecords, context.Variables, context.Images);
        }
        catch (OperationCanceledException)
        {
            runtimeEventSink?.Invoke(new FlowRuntimeEvent
            {
                Kind = FlowRuntimeEventKind.FlowCancelled,
                Elapsed = stopwatch.Elapsed,
                Message = $"流程已停止：已执行 {executedCount} 个节点"
            });

            return FlowExecutionResult.Cancelled(executedCount, stopwatch.Elapsed, nodeRecords, context.Variables, context.Images);
        }
        catch (Exception ex)
        {
            runtimeEventSink?.Invoke(new FlowRuntimeEvent
            {
                Kind = FlowRuntimeEventKind.FlowFailed,
                Elapsed = stopwatch.Elapsed,
                Message = ex.Message
            });

            return FlowExecutionResult.Failed(null, ex.Message, executedCount, stopwatch.Elapsed, nodeRecords, context.Variables, context.Images);
        }
        finally
        {
            stopwatch.Stop();
            lock (gate)
            {
                runCts?.Dispose();
                runCts = null;
                pauseCompletion = null;
                IsPaused = false;
                IsRunning = false;
                PausedNode = null;
            }
        }
    }

    public async Task<FlowNodeExecutionResult> ExecuteNodeInternalAsync(
        FlowNodeViewModel node,
        IEnumerable<FlowConnectionViewModel> connections,
        FlowGraph graph,
        FlowExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var executor = executorRegistry.GetExecutor(node.NodeType);
        if (executor == null)
        {
            return FlowNodeExecutionResult.Failed($"节点类型 {node.NodeType} 未注册执行器。");
        }

        var effectiveContext = context ?? new FlowExecutionContext(graph, false, cancellationToken);

        try
        {
            node.Status = NodeStatus.Running;
            var inputs = CollectInputs(node, connections, effectiveContext);

            var model = graph.Nodes.FirstOrDefault(n => n.Id == node.NodeId);
            if (model == null)
            {
                throw new InvalidOperationException($"节点 '{node.DisplayName}' 数据模型损坏。");
            }

            node.SyncToModel(model);
            var result = await executor.ExecuteAsync(model, effectiveContext, inputs, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                node.Status = NodeStatus.Failed;
                return result;
            }

            DispatchOutputs(node, connections, result.Outputs, result.Overlays, effectiveContext);
            node.Status = NodeStatus.Success;
            return result;
        }
        catch (OperationCanceledException)
        {
            node.Status = NodeStatus.Idle;
            throw;
        }
        catch (Exception ex)
        {
            node.Status = NodeStatus.Failed;
            return FlowNodeExecutionResult.Failed(ex.Message);
        }
    }

    private static void ResetRuntimeState(IEnumerable<FlowNodeViewModel> nodes, IEnumerable<FlowConnectionViewModel> connections)
    {
        foreach (var node in nodes)
        {
            node.Status = NodeStatus.Idle;
            node.ResultValue = null;
            node.LastElapsed = null;
            node.RuntimeMessage = null;
            foreach (var port in node.InputPorts) port.LastValue = null;
            foreach (var port in node.OutputPorts) port.LastValue = null;
            foreach (var port in node.GetVisiblePorts()) port.LastValue = null;
        }

        foreach (var connection in connections)
        {
            connection.LastValue = null;
        }
    }

    private static bool IsNodeReady(FlowNodeViewModel node, IEnumerable<FlowConnectionViewModel> connections)
    {
        return node.InputPorts.All(port =>
        {
            var inputConnections = connections.Where(c => c.Target.PortId == port.PortId).ToList();
            return inputConnections.Count == 0 || inputConnections.All(c => c.LastValue is FlowValue value && value.HasValue);
        });
    }

    private async Task WaitForDebuggerAsync(
        FlowNodeViewModel node,
        IEnumerable<FlowConnectionViewModel> connections,
        bool isDebug,
        Action<FlowRuntimeEvent>? runtimeEventSink,
        CancellationToken cancellationToken)
    {
        if (!isDebug)
        {
            return;
        }

        bool hasIncomingBreakpoint = connections.Any(c => c.Target.Node == node && c.HasBreakpoint);
        if (!stepRequested && !hasIncomingBreakpoint)
        {
            return;
        }

        TaskCompletionSource completion;
        lock (gate)
        {
            IsPaused = true;
            PausedNode = node;
            node.Status = NodeStatus.Paused;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pauseCompletion = completion;
        }

        runtimeEventSink?.Invoke(new FlowRuntimeEvent
        {
            Kind = FlowRuntimeEventKind.DebugPaused,
            Node = node,
            Message = $"调试暂停：{node.DisplayName}"
        });

        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, FlowValue> CollectInputs(
        FlowNodeViewModel node,
        IEnumerable<FlowConnectionViewModel> connections,
        FlowExecutionContext context)
    {
        var inputs = new Dictionary<string, FlowValue>();
        foreach (var inputPort in node.InputPorts)
        {
            var connection = connections.FirstOrDefault(c => c.Target.PortId == inputPort.PortId);
            if (connection?.LastValue is FlowValue value)
            {
                inputs[inputPort.Name] = value;
                inputPort.LastValue = value.Value;
                context.RecordPortValue(
                    node.NodeId.ToString("D"),
                    node.DisplayName,
                    inputPort.Name,
                    PortDirection.Input,
                    inputPort.DataType,
                    value);
                if (value.Value is IVisionImage inputImage)
                {
                    context.RecordImage(
                        node.NodeId.ToString("D"),
                        node.DisplayName,
                        inputPort.Name,
                        PortDirection.Input,
                        inputImage);
                }
            }
            else
            {
                inputs[inputPort.Name] = FlowValue.Missing;
                context.RecordPortValue(
                    node.NodeId.ToString("D"),
                    node.DisplayName,
                    inputPort.Name,
                    PortDirection.Input,
                    inputPort.DataType,
                    FlowValue.Missing);
            }
        }

        return inputs;
    }

    private static void DispatchOutputs(
        FlowNodeViewModel node,
        IEnumerable<FlowConnectionViewModel> connections,
        IReadOnlyDictionary<string, FlowValue> outputs,
        IReadOnlyList<Kwy.Vision.Abstractions.Results.IVisionOverlayShape> overlays,
        FlowExecutionContext context)
    {
        var imageLists = outputs
            .Where(output => output.Value.Value is IEnumerable<IVisionImage>)
            .Select(output => new
            {
                output.Key,
                Images = ((IEnumerable<IVisionImage>)output.Value.Value!).ToArray()
            })
            .ToDictionary(item => item.Key, item => item.Images, StringComparer.OrdinalIgnoreCase);

        foreach (var output in outputs)
        {
            foreach (var outputPort in node.OutputPorts.Where(p => p.Name == output.Key))
            {
                outputPort.LastValue = output.Value.Value;
            }

            foreach (var visiblePort in node.GetVisiblePorts().Where(p => p.Direction == PortDirection.Output && p.Name == output.Key))
            {
                visiblePort.LastValue = output.Value.Value;
            }

            var matchedOutputPort = node.OutputPorts.FirstOrDefault(p => p.Name == output.Key);
            context.RecordPortValue(
                node.NodeId.ToString("D"),
                node.DisplayName,
                output.Key,
                PortDirection.Output,
                matchedOutputPort?.DataType ?? output.Value.DataType ?? PortDataTypes.Any,
                output.Value);
            if (output.Value.Value is IVisionImage outputImage)
            {
                bool duplicatedByCollection = imageLists.Values
                    .Any(images => images.Length > 1 && images.Any(image => ReferenceEquals(image, outputImage)));

                if (!duplicatedByCollection)
                {
                    context.RecordImage(
                        node.NodeId.ToString("D"),
                        node.DisplayName,
                        output.Key,
                        PortDirection.Output,
                        outputImage,
                        overlays);
                }
            }
            else if (output.Value.Value is IEnumerable<IVisionImage> outputImages)
            {
                IVisionImage[] images = imageLists.TryGetValue(output.Key, out IVisionImage[]? cachedImages)
                    ? cachedImages
                    : outputImages.ToArray();

                if (images.Length <= 1)
                {
                    continue;
                }

                int imageIndex = 0;
                foreach (IVisionImage image in images)
                {
                    context.RecordImage(
                        node.NodeId.ToString("D"),
                        node.DisplayName,
                        $"{output.Key}[{imageIndex}]",
                        PortDirection.Output,
                        image,
                        overlays,
                        imageIndex,
                        images.Length);
                    imageIndex++;
                }
            }

            var downstreamConnections = connections
                .Where(c => c.Source.Node == node && c.Source.Name == output.Key)
                .ToList();
            if (downstreamConnections.Count == 0)
            {
                node.ResultValue = output.Value.Value;
            }

            foreach (var connection in downstreamConnections)
            {
                connection.LastValue = output.Value;
                connection.Target.LastValue = output.Value.Value;
            }
        }
    }

    private static FlowNodeRunRecord CreateNodeRecord(
        FlowNodeViewModel node,
        bool success,
        TimeSpan elapsed,
        string? message)
        => new()
        {
            NodeId = node.NodeId.ToString("D"),
            NodeName = node.DisplayName,
            NodeType = node.NodeType,
            Success = success,
            Elapsed = elapsed,
            Message = message
        };
}
