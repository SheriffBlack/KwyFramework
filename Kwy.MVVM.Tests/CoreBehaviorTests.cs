using Kwy.MVVM.Core;
using Kwy.MVVM.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.MVVM.Tests;

public class CoreBehaviorTests
{
    [Fact]
    public void BulkCollectionRestoresNotificationsWhenEnumerationThrows()
    {
        var collection = new BulkObservableCollection<int>();
        int notifications = 0;
        collection.CollectionChanged += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() => collection.AddRange(ThrowingSequence()));
        collection.Add(2);

        Assert.Equal([1, 2], collection);
        Assert.True(notifications >= 2);
    }

    [Fact]
    public void OnDemandModuleInitializesOnlyWhenLoaded()
    {
        DemandModule.Initialized = 0;
        var module = new DemandModule();
        var services = new ServiceCollection().BuildServiceProvider();
        var manager = new ModuleManager(services, [module]);

        Assert.False(manager.IsModuleLoaded(nameof(DemandModule)));

        manager.LoadModule<DemandModule>();
        manager.LoadModule<DemandModule>();

        Assert.True(manager.IsModuleLoaded(nameof(DemandModule)));
        Assert.Equal(1, DemandModule.Initialized);
    }

    [Fact]
    public void PermissionCommandUsesSharedPermissionService()
    {
        var permissionService = new MutablePermissionService();
        bool executed = false;
        var command = new DelegateCommand(() => executed = true)
            .WithPermission(permissionService, "User.Delete");

        Assert.False(command.CanExecute(null));

        command.Execute(null);
        Assert.False(executed);

        permissionService.HasAccess = true;
        permissionService.NotifyPermissionsChanged("User.Delete");

        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.True(executed);
    }

    [Fact]
    public async Task AuthorizationServiceUsesPermissionService()
    {
        var permissionService = new MutablePermissionService();
        var authorizationService = new PermissionAuthorizationService(permissionService);

        var denied = await authorizationService.AuthorizeAsync("Recipe.Edit");
        Assert.False(denied.Succeeded);

        permissionService.HasAccess = true;
        var allowed = await authorizationService.AuthorizeAsync("Recipe.Edit");
        Assert.True(allowed.Succeeded);
    }

    private static IEnumerable<int> ThrowingSequence()
    {
        yield return 1;
        throw new InvalidOperationException("Expected test exception.");
    }

    [Module(OnDemand = true)]
    private sealed class DemandModule : IModule
    {
        public static int Initialized { get; set; }

        public void RegisterTypes(IServiceCollection services)
        {
        }

        public void OnInitialized(IServiceProvider provider) => Initialized++;
    }

    private sealed class MutablePermissionService : IPermissionService
    {
        public bool HasAccess { get; set; }

        public event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

        public bool HasPermission(string permissionCode) => HasAccess;

        public string GetNoPermissionMessage(string permissionCode) => $"Denied: {permissionCode}";

        public void NotifyPermissionsChanged(string permissionCode)
            => PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs(permissionCode));
    }
}
