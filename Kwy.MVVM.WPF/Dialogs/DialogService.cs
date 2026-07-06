using Kwy.MVVM.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// WPF 平台的对话框服务实现。
/// 这个类会把 "View名字" 解析为真正的窗体，塞在 Window 里面展示，并接管 ViewModel 的生命周期。
/// </summary>
public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public DialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Show(string name, IDialogParameters? parameters = null, Action<IDialogResult>? callback = null)
    {
        // 1. 【极致性能】：O(1) 极速拉取 View (抛弃反射扫包)
        var view = _serviceProvider.GetRequiredKeyedService<FrameworkElement>(name);

        // 自动装配 ViewModel (如果 DataContext 为空)
        if (view.DataContext == null)
        {
            Kwy.MVVM.WPF.Mvvm.ViewModelLocator.AutoWire(view);
        }
        var viewModel = view.DataContext as IDialogAware
            ?? throw new InvalidOperationException($"对话框视图 '{name}' 的 DataContext 必须实现 {nameof(IDialogAware)}。");

        // 2. 从 DI 容器获取弹窗的 Window 壳子
        var window = _serviceProvider.GetRequiredService<IDialogWindow>();
        window.Content = view;

        // 3. 【零反射读取】：解析 XAML 附加属性
        var windowStyle = Dialog.GetWindowStyle(view);
        if (windowStyle != null && window is Window w)
        {
            w.Style = windowStyle;
        }

        // 默认居中父体，防止非模态弹窗跑到主程序后面去
        var startupLocation = (WindowStartupLocation)view.GetValue(Dialog.WindowStartupLocationProperty);
        if (window is Window w2)
        {
            w2.WindowStartupLocation = startupLocation;
            w2.Owner = Application.Current?.MainWindow;
        }

        // 4. 【生命周期与极客级防漏防抖】
        if (viewModel != null)
        {
            // 先触发打开事件，传递参数，让 ViewModel 有机会在此时解析 Title
            viewModel.OnDialogOpened(parameters ?? new DialogParameters());

            if (window is Window realWindow)
            {
                // 🚀 核心架构修复：将 Window 的数据上下文也设为 ViewModel，彻底激活 Window 层的 XAML 绑定（如 DataTrigger）
                realWindow.DataContext = viewModel;

                // 建立真正的 WPF 数据绑定，而不是只赋值一次
                realWindow.SetBinding(Window.TitleProperty, new System.Windows.Data.Binding(nameof(IDialogAware.Title)) { Source = viewModel });

                // 【状态锁】：防止代码主动 Close 和 右上角 X 触发两次 callback
                bool isCallbackInvoked = false;

                // 【局部函数】：代替匿名委托，避免不必要的堆分配，且方便精准解绑
                void RequestCloseHandler(IDialogResult result)
                {
                    // 步骤一：立刻断开强引用！
                    viewModel.RequestClose -= RequestCloseHandler;

                    if (!isCallbackInvoked)
                    {
                        isCallbackInvoked = true;
                        callback?.Invoke(result); // 执行业务侧的回调
                    }

                    // 这行代码会连带触发下方的 Closed 事件
                    realWindow.Close();
                }

                // 订阅 ViewModel 的关闭请求
                viewModel.RequestClose += RequestCloseHandler;

                // 拦截关闭前事件
                realWindow.Closing += (s, e) =>
                {
                    if (!viewModel.CanCloseDialog())
                    {
                        e.Cancel = true;
                    }
                };

                // 拦截彻底关闭后事件 (兜底清理)
                void WindowClosedHandler(object? sender, EventArgs e)
                {
                    // 彻底斩断 Window 和 ViewModel 之间的所有事件挂载
                    realWindow.Closed -= WindowClosedHandler;
                    viewModel.RequestClose -= RequestCloseHandler;

                    viewModel.OnDialogClosed();

                    // 【极限兜底】：如果用户没点确定/取消，而是直接点了右上角的 X
                    // 此时业务侧如果不给个交代，很容易死等。我们默认返回 ButtonResult.None
                    if (!isCallbackInvoked)
                    {
                        isCallbackInvoked = true;
                        callback?.Invoke(new DialogResult(ButtonResult.None));
                    }
                }

                realWindow.Closed += WindowClosedHandler;
            }
        }

        // 5. 【非模态释放】：立刻显示，直接跑完当前方法，绝不阻塞当前 UI 线程
        window.Show();
    }

    public Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null)
    {
        var tcs = new TaskCompletionSource<IDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerWindow = Application.Current?.MainWindow;

        // 1. 【极致性能】：使用 .NET 8 Keyed Services 直接以 O(1) 速度解析 View
        var view = _serviceProvider.GetRequiredKeyedService<FrameworkElement>(name);

        // 自动装配 ViewModel (如果 DataContext 为空)
        if (view.DataContext == null)
        {
            Mvvm.ViewModelLocator.AutoWire(view);
        }
        var viewModel = view.DataContext as IDialogAware
            ?? throw new InvalidOperationException($"对话框视图 '{name}' 的 DataContext 必须实现 {nameof(IDialogAware)}。");

        // 2. 【架构融合】：从 DI 容器中解析自定义的弹窗壳子 (DefaultDialogWindow)
        var window = _serviceProvider.GetRequiredService<IDialogWindow>();
        window.Content = view;

        // 3. 【解析 XAML 附加属性】：读取 View 上配置的样式和启动位置
        var windowStyle = Dialog.GetWindowStyle(view);
        if (windowStyle != null && window is Window w)
        {
            w.Style = windowStyle;
        }

        // 默认居中父体，如果 View 上有写 kwy:Dialog.WindowStartupLocation="CenterScreen"，则覆盖
        var startupLocation = (WindowStartupLocation)view.GetValue(Dialog.WindowStartupLocationProperty);
        if (window is Window w2)
        {
            w2.WindowStartupLocation = startupLocation;
            w2.Owner = ownerWindow;
        }

        // 4. 生命周期与内存防漏绑定
        if (viewModel != null)
        {
            // 先触发 ViewModel 打开事件，传递参数，让 ViewModel 解析出 Title
            viewModel.OnDialogOpened(parameters ?? new DialogParameters());

            if (window is Window realWindow)
            {
                // 🚀 核心架构修复：将 Window 的数据上下文也设为 ViewModel，彻底激活 Window 层的 XAML 绑定（如 DataTrigger）
                realWindow.DataContext = viewModel;

                // 建立真正的动态绑定
                realWindow.SetBinding(Window.TitleProperty, new System.Windows.Data.Binding(nameof(IDialogAware.Title)) { Source = viewModel });

                var ownerHitTestChanged = false;
                var previousOwnerHitTestVisible = true;
                var ownerActivationHooked = false;

                void DisableOwnerHitTest()
                {
                    if (ownerWindow == null || ownerWindow == realWindow)
                    {
                        return;
                    }

                    previousOwnerHitTestVisible = ownerWindow.IsHitTestVisible;
                    ownerWindow.IsHitTestVisible = false;
                    ownerHitTestChanged = true;
                }

                void RestoreOwnerHitTest()
                {
                    if (!ownerHitTestChanged || ownerWindow == null)
                    {
                        return;
                    }

                    ownerWindow.IsHitTestVisible = previousOwnerHitTestVisible;
                    ownerHitTestChanged = false;
                }

                void ReturnFocusToDialog()
                {
                    if (!realWindow.IsVisible)
                    {
                        return;
                    }

                    _ = realWindow.Dispatcher.InvokeAsync(() =>
                    {
                        if (!realWindow.IsVisible)
                        {
                            return;
                        }

                        if (realWindow.WindowState == WindowState.Minimized)
                        {
                            realWindow.WindowState = WindowState.Normal;
                        }

                        realWindow.Activate();
                        realWindow.Focus();
                    });
                }

                void OwnerActivatedHandler(object? sender, EventArgs e)
                {
                    ReturnFocusToDialog();
                }

                void HookOwnerActivation()
                {
                    if (ownerWindow == null || ownerWindow == realWindow || ownerActivationHooked)
                    {
                        return;
                    }

                    ownerWindow.Activated += OwnerActivatedHandler;
                    ownerActivationHooked = true;
                }

                void UnhookOwnerActivation()
                {
                    if (!ownerActivationHooked || ownerWindow == null)
                    {
                        return;
                    }

                    ownerWindow.Activated -= OwnerActivatedHandler;
                    ownerActivationHooked = false;
                }

                void RequestCloseHandler(IDialogResult result)
                {
                    viewModel.RequestClose -= RequestCloseHandler;
                    UnhookOwnerActivation();
                    RestoreOwnerHitTest();
                    tcs.TrySetResult(result);
                    realWindow.Close();
                }

                viewModel.RequestClose += RequestCloseHandler;

                realWindow.Closing += (s, e) =>
                {
                    if (!viewModel.CanCloseDialog())
                    {
                        e.Cancel = true;
                    }
                };

                realWindow.Closed += (s, e) =>
                {
                    UnhookOwnerActivation();
                    RestoreOwnerHitTest();
                    viewModel.OnDialogClosed();
                    if (!tcs.Task.IsCompleted)
                    {
                        viewModel.RequestClose -= RequestCloseHandler;
                        tcs.TrySetResult(new DialogResult(ButtonResult.None));
                    }
                };

                // 5. 真正的异步触发
                _ = realWindow.Dispatcher.InvokeAsync(() =>
                {
                    HookOwnerActivation();
                    DisableOwnerHitTest();
                    realWindow.Show();
                });
            }
        }

        // 窗口关闭后，tcs.Task 肯定已经有了 Result
        return tcs.Task;
    }
}
