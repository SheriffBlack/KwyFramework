using KwyTemplate.Vision;
using KwyTemplate.Vision.NodeDescriptors;
using KwyTemplate.Vision.Registries;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.Vision.Tests;

public sealed class VisionModuleTests
{
    [Fact]
    public void VisionModule_RegistersServicesAndDefaultNodes()
    {
        var services = new ServiceCollection();
        var module = new VisionModule();

        module.RegisterTypes(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        module.OnInitialized(provider);

        FlowNodeRegistry nodeRegistry = provider.GetRequiredService<FlowNodeRegistry>();
        FlowNodeExecutorRegistry executorRegistry = provider.GetRequiredService<FlowNodeExecutorRegistry>();

        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Math.Add");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Math.NumberConstant");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Logic.RangeJudgement");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Vision.Input.LocalImage");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Vision.Input.LocalVideo");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Vision.Input.CameraCapture");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Vision.Threshold");
        Assert.Contains(nodeRegistry.All, item => item.NodeType == "Vision.ObjectDetection.Yolo");
        Assert.NotNull(executorRegistry.GetExecutor("Math.Add"));
        Assert.NotNull(executorRegistry.GetExecutor("Math.NumberConstant"));
        Assert.NotNull(executorRegistry.GetExecutor("Logic.RangeJudgement"));
        Assert.NotNull(executorRegistry.GetExecutor("Vision.Input.LocalImage"));
        Assert.NotNull(executorRegistry.GetExecutor("Vision.Input.LocalVideo"));
        Assert.NotNull(executorRegistry.GetExecutor("Vision.Input.CameraCapture"));
        Assert.NotNull(executorRegistry.GetExecutor("Vision.ObjectDetection.Yolo"));

        int nodeCount = nodeRegistry.All.Count;
        module.OnInitialized(provider);
        Assert.Equal(nodeCount, nodeRegistry.All.Count);
    }
}
