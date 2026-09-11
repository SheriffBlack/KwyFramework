using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.UI.WPF.Services.FileDialogs;
using Kwy.Vision.WPF.Images;
using Kwy.Vision.WPF.Sources;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Vision.Cache;
using KwyTemplate.Vision.Executors;
using KwyTemplate.Vision.NodeDescriptors;
using KwyTemplate.Vision.Registries;
using KwyTemplate.Vision.Services;
using KwyTemplate.Vision.ViewModels;
using KwyTemplate.Vision.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KwyTemplate.Vision;

[Module(ModuleName = ModuleNames.VisionModule)]
public class VisionModule : IModule
{
    public void OnInitialized(IServiceProvider containerProvider)
    {
        var registry = containerProvider.GetRequiredService<FlowNodeRegistry>();
        registry.Register(new MathNumberConstantDescriptor());
        registry.Register(new MathAddDescriptor());
        registry.Register(new LogicRangeJudgementDescriptor());
        registry.Register(new LogicDelayDescriptor());
        registry.Register(new IoReadDigitalInputDescriptor());
        registry.Register(new IoWriteDigitalOutputDescriptor());
        registry.Register(new LocalImageInputDescriptor());
        registry.Register(new LocalVideoInputDescriptor());
        registry.Register(new CameraCaptureInputDescriptor());
        registry.Register(new ImagePreprocessDescriptor());
        registry.Register(new ThresholdSegmentationDescriptor());
        registry.Register(new BlobInspectionDescriptor());
        registry.Register(new CaliperMeasurementDescriptor());
        registry.Register(new LineFittingDescriptor());
        registry.Register(new CircleFittingDescriptor());
        registry.Register(new TemplateMatchingDescriptor());
        registry.Register(new YoloObjectDetectionDescriptor());

        var executorRegistry = containerProvider.GetRequiredService<FlowNodeExecutorRegistry>();
        executorRegistry.Register(new MathNumberConstantExecutor());
        executorRegistry.Register(new MathAddExecutor());
        executorRegistry.Register(new LogicRangeJudgementExecutor());
        executorRegistry.Register(new LogicDelayExecutor());
        executorRegistry.Register(new VisionLocalImageInputExecutor(containerProvider));
        executorRegistry.Register(new VisionLocalVideoInputExecutor(containerProvider));
        executorRegistry.Register(new VisionCameraCaptureInputExecutor(containerProvider));
        executorRegistry.Register(new VisionImagePreprocessExecutor(containerProvider));
        executorRegistry.Register(new VisionThresholdExecutor(containerProvider));
        executorRegistry.Register(new VisionBlobExecutor(containerProvider));
        executorRegistry.Register(new VisionCaliperExecutor(containerProvider));
        executorRegistry.Register(new VisionLineFittingExecutor(containerProvider));
        executorRegistry.Register(new VisionCircleFittingExecutor(containerProvider));
        executorRegistry.Register(new VisionTemplateMatchingExecutor(containerProvider));
        executorRegistry.Register(new VisionYoloObjectDetectionExecutor(containerProvider));
    }

    public void RegisterTypes(IServiceCollection containerRegistry)
    {
        containerRegistry.AddSingleton<FlowNodeRegistry>();
        containerRegistry.AddSingleton<FlowNodeExecutorRegistry>();
        containerRegistry.AddSingleton<FlowPersistenceService>();
        containerRegistry.AddSingleton<RecentProjectService>();
        containerRegistry.AddSingleton<DataTypeColorService>();
        containerRegistry.AddSingleton<FlowExecutionService>();
        containerRegistry.AddSingleton<FlowLayoutService>();
        containerRegistry.AddSingleton<VisionImageCache>();
        containerRegistry.TryAddSingleton<ILocalVisionImageFactory, LocalVisionImageFactory>();
        containerRegistry.TryAddSingleton<IVisionFrameSourceFactory, VisionFrameSourceFactory>();
        containerRegistry.TryAddSingleton<IFileDialogService, WpfFileDialogService>();

        containerRegistry.RegisterForNavigation<FlowEditorView, FlowEditorViewModel>();
        containerRegistry.RegisterForNavigation<NodePaletteView, NodePaletteViewModel>();
        containerRegistry.RegisterForNavigation<PropertySettingsView, PropertySettingsViewModel>();
    }
}
