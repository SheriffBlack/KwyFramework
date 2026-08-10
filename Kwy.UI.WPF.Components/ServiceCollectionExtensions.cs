using Kwy.MVVM.WPF.Dialogs;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Components.Logging;
using Kwy.UI.WPF.Components.Toasts;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.UI.WPF.Components;

/// <summary>
/// Kwy WPF 组合组件的注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册标准消息、输入对话框、Toast 和默认 Kwy 对话框窗口。
    /// </summary>
    public static IServiceCollection AddKwyWpfComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKwyWpfServices();
        services.AddSingleton<KwyLogService>();
        services.AddSingleton<IDialogMessageService, DialogMessageService>();
        services.AddSingleton<IInputDialogService, InputDialogService>();
        services.AddSingleton<IToastMessageService, ToastMessageService>();
        services.AddTransient<IDialogWindow, KwyDialogWindow>();
        services.RegisterForNavigation<DialogMessageView, DialogMessageViewModel>();
        services.RegisterForNavigation<InputDialogView, InputDialogViewModel>();

        return services;
    }
}

