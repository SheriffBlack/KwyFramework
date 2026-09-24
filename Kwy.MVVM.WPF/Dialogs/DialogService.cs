using Kwy.MVVM.Dialogs;
using Kwy.MVVM.WPF.Mvvm;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// WPF 平台的对话框服务实现。
/// 负责解析对话框视图、装配 ViewModel，并管理窗口生命周期。
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider serviceProvider;

    public DialogService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public void Show(string name, IDialogParameters? parameters = null, Action<IDialogResult>? callback = null)
    {
        if (!TryDispatchToApplicationUiThread(() => Show(name, parameters, callback)))
        {
            var session = CreateSession(name, callback, isModal: false);
            session.Start(parameters ?? new DialogParameters());
        }
    }

    public Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            return dispatcher.InvokeAsync(
                () => ShowDialogAsync(name, parameters),
                DispatcherPriority.Send).Task.Unwrap();
        }

        var completion = new TaskCompletionSource<IDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var session = CreateSession(
                name,
                result => completion.TrySetResult(result),
                isModal: true,
                failure => completion.TrySetException(failure));
            session.Start(parameters ?? new DialogParameters());
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        return completion.Task;
    }

    private bool TryDispatchToApplicationUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return false;
        }

        _ = dispatcher.BeginInvoke(action, DispatcherPriority.Send);
        return true;
    }

    private DialogSession CreateSession(
        string name,
        Action<IDialogResult>? completed,
        bool isModal,
        Action<Exception>? failed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        FrameworkElement view = serviceProvider.GetRequiredKeyedService<FrameworkElement>(name);
        if (view.DataContext == null)
        {
            ViewModelLocator.AutoWire(view);
        }

        var viewModel = view.DataContext as IDialogAware
            ?? throw new InvalidOperationException($"对话框视图 '{name}' 的 DataContext 必须实现 {nameof(IDialogAware)}。");
        IDialogWindow dialogWindow = serviceProvider.GetRequiredService<IDialogWindow>();
        if (dialogWindow is not Window window)
        {
            throw new InvalidOperationException($"{nameof(IDialogWindow)} 的实现必须继承 {nameof(Window)}。");
        }

        window.Content = view;
        window.DataContext = viewModel;
        window.Owner = ResolveOwnerWindow();
        window.WindowStartupLocation = Dialog.GetWindowStartupLocation(view);
        window.SetBinding(Window.TitleProperty, new Binding(nameof(IDialogAware.Title)) { Source = viewModel });

        if (Dialog.GetWindowStyle(view) is Style style)
        {
            window.Style = style;
        }

        return new DialogSession(window, viewModel, completed, failed, isModal);
    }

    private static Window? ResolveOwnerWindow()
    {
        if (Application.Current == null)
        {
            return null;
        }

        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current.MainWindow;
    }

    private sealed class DialogSession
    {
        private readonly Window window;
        private readonly IDialogAware viewModel;
        private readonly Action<IDialogResult>? completed;
        private readonly Action<Exception>? failed;
        private readonly bool isModal;
        private IDisposable? ownerModalLease;
        private bool hasBeenShown;
        private bool isCloseRequested;
        private bool isCompleted;
        private IDialogResult? requestedResult;

        public DialogSession(
            Window window,
            IDialogAware viewModel,
            Action<IDialogResult>? completed,
            Action<Exception>? failed,
            bool isModal)
        {
            this.window = window;
            this.viewModel = viewModel;
            this.completed = completed;
            this.failed = failed;
            this.isModal = isModal;
        }

        public void Start(IDialogParameters parameters)
        {
            AttachHandlers();
            try
            {
                viewModel.OnDialogOpened(parameters);
                if (isCloseRequested)
                {
                    Complete(requestedResult ?? new DialogResult(ButtonResult.None));
                    return;
                }

                if (isModal)
                {
                    ownerModalLease = DialogOwnerModalCoordinator.Acquire(window.Owner, window);
                }

                hasBeenShown = true;
                window.Show();

                if (!isCompleted && window.IsVisible)
                {
                    window.Activate();
                    window.Focus();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void AttachHandlers()
        {
            viewModel.RequestClose += OnRequestClose;
            window.Closing += OnWindowClosing;
            window.Closed += OnWindowClosed;
        }

        private void DetachHandlers()
        {
            viewModel.RequestClose -= OnRequestClose;
            window.Closing -= OnWindowClosing;
            window.Closed -= OnWindowClosed;
        }

        private void OnRequestClose(IDialogResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!window.Dispatcher.CheckAccess())
            {
                _ = window.Dispatcher.BeginInvoke(() => OnRequestClose(result), DispatcherPriority.Send);
                return;
            }

            if (isCompleted || isCloseRequested)
            {
                return;
            }

            isCloseRequested = true;
            requestedResult = result;
            if (hasBeenShown)
            {
                window.Close();
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (viewModel.CanCloseDialog())
            {
                return;
            }

            e.Cancel = true;
            isCloseRequested = false;
            requestedResult = null;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
            => Complete(requestedResult ?? new DialogResult(ButtonResult.None));

        private void Complete(IDialogResult result)
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            ownerModalLease?.Dispose();
            ownerModalLease = null;
            DetachHandlers();
            try
            {
                viewModel.OnDialogClosed();
            }
            finally
            {
                completed?.Invoke(result);
            }
        }

        private void Fail(Exception exception)
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            ownerModalLease?.Dispose();
            ownerModalLease = null;
            DetachHandlers();
            try
            {
                viewModel.OnDialogClosed();
            }
            finally
            {
                if (failed == null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }
                else
                {
                    failed(exception);
                }
            }
        }
    }
}
