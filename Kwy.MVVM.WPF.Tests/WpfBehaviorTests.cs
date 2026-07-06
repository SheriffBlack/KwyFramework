using Kwy.MVVM.Dialogs;
using Kwy.MVVM.Regions;
using Kwy.MVVM.WPF.Dialogs;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.MVVM.WPF.Permissions;
using Kwy.MVVM.WPF.Regions;
using Kwy.MVVM.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Kwy.MVVM.WPF.Tests;

public class WpfBehaviorTests
{
    [Fact]
    public Task NavigationRejectsAViewOwnedByAnotherRegion() => StaTest.RunAsync(async () =>
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<FrameworkElement, TestView>(nameof(TestView));
        using var provider = services.BuildServiceProvider();
        using var manager = new RegionManager(provider);

        manager.RegisterViewWithRegion("MainRegion", typeof(TestView));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RequestNavigateAsync("SideRegion", nameof(TestView)));
    });

    [Fact]
    public Task NavigationIsSerializedPerRegion() => StaTest.RunAsync(async () =>
    {
        var slowViewModel = new SlowNavigationViewModel();
        var slowView = new SlowView { DataContext = slowViewModel };
        var nextView = new NextView();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<FrameworkElement>(nameof(SlowView), slowView);
        services.AddKeyedSingleton<FrameworkElement>(nameof(NextView), nextView);
        using var provider = services.BuildServiceProvider();
        using var manager = new RegionManager(provider);
        var region = new ContentControl();
        RegionManager.SetRegionName(region, "MainRegion");
        region.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        var firstNavigation = manager.RequestNavigateAsync("MainRegion", nameof(SlowView));
        await slowViewModel.Started.Task;
        var secondNavigation = manager.RequestNavigateAsync("MainRegion", nameof(NextView));

        Assert.Same(slowView, region.Content);
        Assert.False(secondNavigation.IsCompleted);

        slowViewModel.Release();
        Assert.True((await firstNavigation).Result);
        Assert.True((await secondNavigation).Result);
        Assert.Same(nextView, region.Content);
    });

    [Fact]
    public Task ViewCacheDisposesViewAndViewModel() => StaTest.RunAsync(() =>
    {
        var viewModel = new DisposableViewModel();
        var view = new DisposableView { DataContext = viewModel };
        using var cache = new ViewCacheManager();

        cache.GetOrCreateView(nameof(DisposableView), () => view, "MainRegion");
        cache.RemoveView(nameof(DisposableView));

        Assert.True(view.IsDisposed);
        Assert.True(viewModel.IsDisposed);
        Assert.Null(view.DataContext);
        return Task.CompletedTask;
    });

    [Fact]
    public Task DialogCompletesWhenViewModelRequestsClose() => StaTest.RunAsync(async () =>
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<FrameworkElement, DialogView>(nameof(DialogView));
        services.AddTransient<DialogViewModel>();
        services.AddTransient<IDialogWindow, DefaultDialogWindow>();
        using var provider = services.BuildServiceProvider();
        ViewModelLocator.SetDefaultServiceProvider(provider);
        ViewModelLocator.Register(typeof(DialogView), typeof(DialogViewModel));

        try
        {
            var service = new DialogService(provider);
            var result = await service.ShowDialogAsync(nameof(DialogView));
            Assert.Equal(ButtonResult.OK, result.Result);
        }
        finally
        {
            ViewModelLocator.SetDefaultServiceProvider(new ServiceCollection().BuildServiceProvider());
        }
    });

    [Fact]
    public Task PermissionRestoresOriginalElementState() => StaTest.RunAsync(() =>
    {
        var permissionService = new MutablePermissionService();
        var button = new Button
        {
            IsEnabled = false,
            Visibility = Visibility.Hidden
        };

        Permission.SetService(button, permissionService);
        Permission.SetMode(button, PermissionCheckMode.Both);
        Permission.SetPolicy(button, "Edit");
        Assert.Equal(Visibility.Collapsed, button.Visibility);

        permissionService.HasAccess = true;
        permissionService.NotifyPermissionsChanged("Edit");

        Assert.False(button.IsEnabled);
        Assert.Equal(Visibility.Hidden, button.Visibility);

        return Task.CompletedTask;
    });

    public sealed class TestView : UserControl
    {
    }

    public sealed class SlowView : UserControl
    {
    }

    public sealed class NextView : UserControl
    {
    }

    public sealed class SlowNavigationViewModel : IAsyncNavigationAware
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public Task OnNavigatedFromAsync(NavigationContext navigationContext) => Task.CompletedTask;

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
            Started.TrySetResult();
            await release.Task;
        }

        public void Release() => release.TrySetResult();
    }

    public sealed class DisposableView : UserControl, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    public sealed class DisposableViewModel : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    public sealed class DialogView : UserControl
    {
    }

    public sealed class DialogViewModel : IDialogAware
    {
        public string Title => "Test";
        public event Action<IDialogResult>? RequestClose;
        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                () => RequestClose?.Invoke(new DialogResult(ButtonResult.OK)));
        }
    }

    private sealed class MutablePermissionService : IPermissionService
    {
        public bool HasAccess { get; set; }
        public event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;
        public bool HasPermission(string permissionCode) => HasAccess;
        public string GetNoPermissionMessage(string permissionCode) => "Denied";
        public void NotifyPermissionsChanged(string permissionCode)
            => PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs(permissionCode));
    }
}

internal static class StaTest
{
    public static Task RunAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                }
            });

            System.Windows.Threading.Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
