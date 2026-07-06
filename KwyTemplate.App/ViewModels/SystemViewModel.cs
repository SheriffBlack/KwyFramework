using System.Collections.ObjectModel;
using Kwy.MVVM.Core;
using KwyTemplate.App.Models;
using KwyTemplate.Device.Connections;
using KwyTemplate.Device.Options;

namespace KwyTemplate.App.ViewModels;

public sealed class SystemViewModel : BindableBase
{
    private readonly IDeviceConnectionOptionsStore optionsStore;
    private readonly IReadOnlyDictionary<string, IDeviceConnectionFactory> factories;
    private DeviceConnectionOptions? deviceOptions;
    private string statusMessage = string.Empty;

    public SystemViewModel(
        IDeviceConnectionOptionsStore optionsStore,
        IEnumerable<IDeviceConnectionFactory> factories)
    {
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.factories = factories?.ToDictionary(x => x.DeviceType, StringComparer.OrdinalIgnoreCase)
            ?? throw new ArgumentNullException(nameof(factories));
        _ = LoadAsync();
    }

    public ObservableCollection<ConfigurationSectionModel> ConfigurationSections { get; } = new();

    public DeviceConnectionOptions? DeviceOptions
    {
        get => deviceOptions;
        private set => SetProperty(ref deviceOptions, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    private DelegateCommand? saveCommand;

    public DelegateCommand SaveCommand => saveCommand ??= new DelegateCommand(async () => await SaveAsync());

    private DelegateCommand? reloadCommand;

    public DelegateCommand ReloadCommand => reloadCommand ??= new DelegateCommand(async () => await LoadAsync());

    private async Task LoadAsync()
    {
        DeviceConnectionOptions options = await optionsStore.LoadAsync(DestroyToken);
        DeviceOptions = options;

        ConfigurationSections.Clear();
        foreach (DeviceConnectionEntry entry in options.Devices.Where(static x => x.Enabled))
        {
            ConfigurationSections.Add(CreateConfigurationSection(entry));
        }

        StatusMessage = "连接配置已加载";
    }

    private ConfigurationSectionModel CreateConfigurationSection(DeviceConnectionEntry entry)
    {
        if (!factories.TryGetValue(entry.DeviceType, out IDeviceConnectionFactory? factory))
        {
            return new ConfigurationSectionModel(
                string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.DeviceId : entry.DisplayName,
                $"设备类型：{entry.DeviceType}。该设备类型尚未注册连接工厂，显示连接条目基础信息。",
                entry);
        }

        DeviceConnectionConfigurationSection section = factory.CreateConfigurationSection(entry);
        return new ConfigurationSectionModel(section.Title, section.Description, section.Source);
    }

    private async Task SaveAsync()
    {
        if (DeviceOptions == null)
        {
            return;
        }

        await optionsStore.SaveAsync(DeviceOptions, DestroyToken);
        StatusMessage = $"连接配置已保存：{DateTime.Now:HH:mm:ss}";
    }
}
