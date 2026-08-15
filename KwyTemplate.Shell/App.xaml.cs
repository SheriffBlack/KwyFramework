using Kwy.MVVM.Modularity;
using Kwy.Device.Abstractions.Equipment;
using Kwy.Logging.Serilog;
using Kwy.Logging.Abstractions;
using Kwy.MVVM.WPF;
using Kwy.MVVM.WPF.Dialogs;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.MVVM.WPF.Regions;
using Kwy.UI.WPF.Components;
using Kwy.UI.WPF.Components.Logging;
using KwyTemplate.App;
using KwyTemplate.App.Services;
using KwyTemplate.App.Views;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device;
using KwyTemplate.Flow;
using KwyTemplate.MES.Cyntec;
using KwyTemplate.Security;
using KwyTemplate.Security.Options;
using KwyTemplate.Security.SuperDog;
using KwyTemplate.Shell.Services;
using KwyTemplate.Shell.Views;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KwyTemplate.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : KwyApplication
{
    private Window? startupWindow;
    private bool startupProgressLogWired;
    private bool globalExceptionLogWired;
    private bool viewCacheLogWired;

    public App()
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveKwyAssembly;
    }

    private static Assembly? ResolveKwyAssembly(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        string candidate = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }

    protected override void OnServiceProviderCreated(IServiceProvider serviceProvider)
    {
        WireStartupProgressLog(serviceProvider);
        WireGlobalExceptionLog(serviceProvider);
        ProgramSettingsLoadResult settingsLoadResult = serviceProvider.GetRequiredService<ProgramSettingsStore>().LoadOrCreate();
        ILocalizationService localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        localizationService.Apply(settingsLoadResult.Settings.Language);
        serviceProvider.GetRequiredService<StartupProgressService>().Report(
            localizationService.T("Startup.Program.LoadingSettings", "Loading program settings..."),
            5);
        ShowStartupWindow();
    }

    private void WireGlobalExceptionLog(IServiceProvider serviceProvider)
    {
        if (globalExceptionLogWired)
        {
            return;
        }

        var developerLog = serviceProvider.GetRequiredService<ILogService>();
        DispatcherUnhandledException += (_, args) =>
        {
            developerLog.Error("Unhandled UI exception.", args.Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            developerLog.Error("Unobserved task exception.", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                developerLog.Fatal("Unhandled application exception.", exception);
                return;
            }

            developerLog.Fatal($"Unhandled application exception object: {args.ExceptionObject}");
        };
        globalExceptionLogWired = true;
    }

    private void WireStartupProgressLog(IServiceProvider serviceProvider)
    {
        if (startupProgressLogWired)
        {
            return;
        }

        var startupProgress = serviceProvider.GetRequiredService<StartupProgressService>();
        var logService = serviceProvider.GetRequiredService<KwyLogService>();
        var userLogFile = serviceProvider.GetRequiredService<UserVisibleLogFileService>();
        var developerLog = serviceProvider.GetRequiredService<ILogService>();
        startupProgress.ProgressChanged += (_, args) =>
        {
            string message = $"启动进度 {args.ProgressValue:0}%：{args.CurrentItem}";
            string level = "Info";
            if (args.IsCompleted)
            {
                level = "Success";
            }
            else if (args.CurrentItem.Contains("失败", StringComparison.Ordinal) || args.CurrentItem.Contains("异常", StringComparison.Ordinal))
            {
                level = "Error";
            }

            logService.AddStartupProgress(level, message, args.ProgressValue);
            userLogFile.Add(level, message);
            WriteStartupDeveloperLog(developerLog, level, message);
        };
        startupProgressLogWired = true;
    }

    private static void WriteStartupDeveloperLog(ILogService developerLog, string level, string message)
    {
        if (string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
        {
            developerLog.Error(message);
            return;
        }

        developerLog.Info(message);
    }

    protected override Window CreateShell()
    {
        IServiceProvider provider = CurrentServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
        return provider.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IServiceCollection services)
    {
        services.AddKwyWpfComponents();
        services.AddKwySerilogLogging(options =>
        {
            options.LogDirectory = Path.Combine("Logs", "Developer");
            options.FileNamePrefix = string.Empty;
            options.RetainedFileCountLimit = 31;
            options.MinimumLevel = Kwy.Logging.Abstractions.LogLevel.Info;
            options.ApplicationName = "KwyTemplate";
        });
        services.AddSingleton<ProgramSettingsStore>();
        services.AddSingleton<ILocalizationService, ResourceDictionaryLocalizationService>();
        services.AddSingleton<StartupProgressService>();
        services.AddSingleton<UserVisibleLogFileService>();
        services.AddSingleton<IEquipmentEventSink, LogEquipmentEventSink>();

        services.AddSingleton(new SecuritySessionOptions
        {
            ElevatedUserSessionDuration = TimeSpan.FromMinutes(1)
        });
    }

    protected override void OnInitialized()
    {
        if (CurrentServiceProvider is { } provider)
        {
            WireViewCacheLog(provider);
            provider.GetService<StartupProgressService>()?.Complete();
        }

        CloseStartupWindow();
        base.OnInitialized();
    }

    private void WireViewCacheLog(IServiceProvider serviceProvider)
    {
        if (viewCacheLogWired)
        {
            return;
        }

        IViewCacheManager? cacheManager = serviceProvider.GetService<IViewCacheManager>();
        if (cacheManager == null)
        {
            return;
        }

        cacheManager.DefaultCacheExpiration = TimeSpan.FromHours(4);
        cacheManager.CleanupInterval = TimeSpan.FromHours(1);

        var logService = serviceProvider.GetRequiredService<KwyLogService>();
        var userLogFile = serviceProvider.GetRequiredService<UserVisibleLogFileService>();
        var developerLog = serviceProvider.GetRequiredService<ILogService>();

        cacheManager.ViewCached += item =>
        {
            string message = string.Concat("[Cache] ", item.ViewName);
            logService.Info(message);
            userLogFile.Add("Info", message);
            developerLog.Info("[Cache] View cached: {View}", item.ViewName);
        };

        cacheManager.ViewRestored += item =>
        {
            string message = string.Concat("[Cache] ", item.ViewName);
            logService.Info(message);
            userLogFile.Add("Info", message);
            developerLog.Info("[Cache] View restored: {View}", item.ViewName);
        };

        cacheManager.ViewDestroyed += item =>
        {
            string message = string.Concat("[Cache] ", item.ViewName);
            logService.Warn(message);
            userLogFile.Add("Warn", message);
            developerLog.Warning("[Cache] View destroyed: {View}", item.ViewName);
        };

        viewCacheLogWired = true;
    }

    private void ShowStartupWindow()
    {
        if (startupWindow != null)
        {
            return;
        }

        var loadView = new LoadView();
        startupWindow = new Window
        {
            Content = loadView,
            WindowStartupLocation = Dialog.GetWindowStartupLocation(loadView),
            ShowInTaskbar = false,
            Topmost = true
        };

        Style? windowStyle = Dialog.GetWindowStyle(loadView);
        if (windowStyle != null)
        {
            startupWindow.Style = windowStyle;
        }

        startupWindow.Show();
    }

    private void CloseStartupWindow()
    {
        Window? window = startupWindow;
        startupWindow = null;
        window?.Close();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SecurityModule>();
        moduleCatalog.AddModule<SuperDogSecurityModule>();
        moduleCatalog.AddModule<DeviceModule>();
        moduleCatalog.AddModule<FlowModule>();
        moduleCatalog.AddModule<MesCyntecModule>();
        moduleCatalog.AddModule<AppModule>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownRuntimeServices();
        CloseStartupWindow();
        base.OnExit(e);
    }

    private static void ShutdownRuntimeServices()
    {
        IServiceProvider? provider = CurrentServiceProvider;
        if (provider == null)
        {
            return;
        }

        ILogService? developerLog = provider.GetService<ILogService>();
        developerLog?.Info("[Shutdown] Runtime shutdown started.");

        RunShutdownStep(developerLog, "MachineRuntimeOrchestrator.Stop", static serviceProvider =>
        {
            serviceProvider.GetService<KwyTemplate.App.Orchestration.MachineRuntimeOrchestrator>()?.Stop();
        }, provider);


        RunShutdownStep(developerLog, "IDeviceStartupConnector.Dispose", static serviceProvider =>
        {
            serviceProvider.GetService<KwyTemplate.Device.Devices.IDeviceStartupConnector>()?.Dispose();
        }, provider);

        RunShutdownStep(developerLog, "IDeviceRegistry.Dispose", static serviceProvider =>
        {
            serviceProvider.GetService<Kwy.Device.Abstractions.IDeviceRegistry>()?.Dispose();
        }, provider);

        developerLog?.Info("[Shutdown] Runtime shutdown finished.");
    }

    private static void RunShutdownStep(ILogService? developerLog, string stepName, Action<IServiceProvider> action, IServiceProvider provider)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        developerLog?.Info("[Shutdown] {Step} started.", stepName);
        try
        {
            action(provider);
            developerLog?.Info("[Shutdown] {Step} finished in {ElapsedMilliseconds} ms.", stepName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            developerLog?.Error($"[Shutdown] {stepName} failed in {stopwatch.ElapsedMilliseconds} ms.", ex);
        }
    }
}

