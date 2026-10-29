using Kwy.Device.Abstractions;
using Kwy.Device.Core.Equipment;
using KwyTemplate.Device.Profiles;

namespace KwyTemplate.Device.Devices;

public sealed class DeviceRegistryInitializer : IDeviceRegistryInitializer, IDisposable, IAsyncDisposable
{
    private readonly IDeviceRegistry deviceRegistry;
    private readonly IDeviceEventForwarder deviceEventForwarder;
    private readonly IServiceProvider services;
    private readonly IEnumerable<IDeviceCatalog> catalogs;
    private readonly DeviceCatalogSelectionOptions selectionOptions;
    private bool initialized;
    private readonly List<IDevice> ownedDevices = [];
    private int disposed;

    public DeviceRegistryInitializer(
        IDeviceRegistry deviceRegistry,
        IDeviceEventForwarder deviceEventForwarder,
        IServiceProvider services,
        IEnumerable<IDeviceCatalog> catalogs,
        DeviceCatalogSelectionOptions selectionOptions)
    {
        this.deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        this.deviceEventForwarder = deviceEventForwarder ?? throw new ArgumentNullException(nameof(deviceEventForwarder));
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        this.selectionOptions = selectionOptions ?? throw new ArgumentNullException(nameof(selectionOptions));
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        IDeviceCatalog selectedCatalog = SelectCatalog(catalogs, selectionOptions.ActiveCatalogKey);
        var definitions = selectedCatalog
            .CreateDeviceDefinitions()
            .Where(static definition => definition is not null)
            .ToArray();

        foreach (var group in definitions.GroupBy(static definition => definition.DeviceId, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                throw new InvalidOperationException($"Duplicated device id: {group.Key}.");
            }
        }

        foreach (DeviceDefinition definition in definitions)
        {
            IDevice device = definition.CreateDevice(services);
            try
            {
                deviceEventForwarder.Attach(device);
                deviceRegistry.Add(device);
                ownedDevices.Add(device);
            }
            catch
            {
                deviceEventForwarder.Detach(device);
                device.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        foreach (IDevice device in ownedDevices)
        {
            deviceEventForwarder.Detach(device);
            device.Dispose();
        }

        ownedDevices.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        foreach (IDevice device in ownedDevices)
        {
            deviceEventForwarder.Detach(device);
            await device.DisposeAsync().ConfigureAwait(false);
        }

        ownedDevices.Clear();
    }

    private static IDeviceCatalog SelectCatalog(IEnumerable<IDeviceCatalog> source, string? activeCatalogKey)
    {
        IDeviceCatalog[] catalogArray = source.ToArray();
        if (catalogArray.Length == 0)
        {
            throw new InvalidOperationException("No device catalog is registered.");
        }

        foreach (var group in catalogArray.GroupBy(static catalog => catalog.CatalogKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                throw new InvalidOperationException($"Duplicated device catalog key: {group.Key}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(activeCatalogKey))
        {
            IDeviceCatalog? selected = catalogArray.FirstOrDefault(catalog =>
                string.Equals(catalog.CatalogKey, activeCatalogKey, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
            {
                return selected;
            }

            throw new InvalidOperationException($"Device catalog not found: {activeCatalogKey}.");
        }

        IDeviceCatalog? defaultCatalog = catalogArray.FirstOrDefault(static catalog => catalog.IsDefault);
        return defaultCatalog ?? catalogArray[0];
    }
}
