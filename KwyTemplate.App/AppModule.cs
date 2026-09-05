using Kwy.MVVM.Modularity;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.UI.WPF.Components.Logging;
using KwyTemplate.App.Input;
using KwyTemplate.App.Dialogs;
using KwyTemplate.App.Models;
using KwyTemplate.App.Orchestration;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.App.ViewModels;
using KwyTemplate.App.Views;
using KwyTemplate.Contracts.Modularity;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace KwyTemplate.App;

[Module(ModuleName = ModuleNames.AppModule)]
public class AppModule : IModule
{
    public void OnInitialized(IServiceProvider containerProvider)
    {
        containerProvider.GetRequiredService<ProgramSettingsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<CompensateOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<StandardLimitAutoGenerationOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<BraidOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<MarkPrintOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<ParameterDictOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<ProductionDataOptionsStore>().LoadOrCreate();
        containerProvider.GetRequiredService<MachineRuntimeOrchestrator>().Start();
    }

    public void RegisterTypes(IServiceCollection containerRegistry)
    {
        containerRegistry.AddSingleton<CompensateOptionsStore>();
        containerRegistry.AddSingleton<StandardLimitAutoGenerationOptionsStore>();
        containerRegistry.AddSingleton<StandardLimitAutoGenerationService>();
        containerRegistry.AddSingleton<BraidOptionsStore>();
        containerRegistry.AddSingleton<MarkPrintOptionsStore>();
        containerRegistry.AddSingleton<ParameterDictOptionsStore>();
        containerRegistry.AddSingleton<ProductionDataOptionsStore>();
        containerRegistry.AddSingleton<IProductionDataArchiveService, ProductionDataArchiveService>();
        containerRegistry.AddSingleton<LocalWorkOrderRecipeMapper>();
        containerRegistry.AddSingleton<LocalWorkOrderRecipeRuntimeSnapshotFactory>();
        containerRegistry.AddSingleton<LocalWorkOrderRecipeStore>();
        containerRegistry.AddSingleton<LocalWorkOrderRecipeSession>();
        containerRegistry.AddSingleton<ParameterDictSelectionSession>();
        containerRegistry.AddSingleton<MachineProfileEditorStore>();
        containerRegistry.AddSingleton<RawInputBarcodeOptions>();
        containerRegistry.AddSingleton<IRawInputBarcodeReceiver, RawInputBarcodeReceiver>();
        containerRegistry.AddSingleton<IProductionContext, ProductionContext>();
        containerRegistry.AddSingleton<IProductionRuntimeContext>(provider => provider.GetRequiredService<IProductionContext>());
        containerRegistry.AddSingleton<IPrimaryNavigationState, PrimaryNavigationState>();
        containerRegistry.AddSingleton<MesConnectionStatus>();
        containerRegistry.AddSingleton<StandardSampleState>();
        containerRegistry.AddSingleton<ICorrectionParameterProvider, CorrectionParameterProvider>();
        containerRegistry.AddSingleton<StationEnableStateStore>();
        containerRegistry.AddSingleton<IAppNotificationService, AppNotificationService>();
        containerRegistry.AddSingleton<ICameraStartupOptionsDialogService, CameraStartupOptionsDialogService>();
        containerRegistry.AddSingleton<IApplicationCloseGuard, ApplicationCloseGuard>();
        containerRegistry.AddSingleton<MachineRuntimeOrchestrator>();
        containerRegistry.AddSingleton<ICyntecReelScanWorkflow, CyntecReelScanWorkflow>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, MesConnectionFeature>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, MachineOnlineSignalFeature>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, MachinePlcStopSignalFeature>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, CyntecReelScanFeature>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, StandardSampleExpirationMonitorFeature>();
        containerRegistry.AddSingleton<IMachineRuntimeFeature, CompensateScheduleMonitorFeature>();

        containerRegistry.RegisterForNavigation<CompensateView, CompensateViewModel>();
        containerRegistry.RegisterForNavigation<CorrectionView, CorrectionViewModel>();
        containerRegistry.RegisterForNavigation<ConnectView, ConnectViewModel>();
        containerRegistry.RegisterForNavigation<DiView, DiViewModel>();
        containerRegistry.RegisterForNavigation<DoView, DoViewModel>();
        containerRegistry.RegisterForNavigation<HomeView, HomeViewModel>();
        containerRegistry.RegisterForNavigation<MainView, MainViewModel>();
        containerRegistry.RegisterForNavigation<PlcPointView, PlcPointViewModel>();
        containerRegistry.RegisterForNavigation<SetView, SetViewModel>();
        containerRegistry.RegisterForNavigation<StandardView, StandardViewModel>();
        containerRegistry.RegisterForNavigation<StationView, StationViewModel>();
        containerRegistry.RegisterForNavigation<SystemView, SystemViewModel>();
        containerRegistry.RegisterForNavigation<ParameterDictView, ParameterDictViewModel>();
        containerRegistry.RegisterForNavigation<CameraStartupOptionsDialogView, CameraStartupOptionsDialogViewModel>();
        RegisterLogNavigationView(containerRegistry);
    }

    private static void RegisterLogNavigationView(IServiceCollection services)
    {
        services.AddKeyedTransient<FrameworkElement>(ViewNames.LogView, static (provider, _) =>
        {
            var logService = provider.GetRequiredService<KwyLogService>();
            return new KwyLogListView
            {
                ItemsSource = logService.Entries,
                AutoScroll = true,
                ShowTime = true,
                ShowLevel = true
            };
        });
    }
}




