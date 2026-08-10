using Kwy.Device.Abstractions;
using System.Reflection;

namespace Kwy.Device.Core.Instrument;

public class ConfigFactory
{
    private static readonly Dictionary<string, Type> modelRegistry = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object syncRoot = new();
    private static bool loadedAssembliesScanned;

    public static void RegisterConfig<TConfig>(string supportedModel)
        where TConfig : IDeviceConfig, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supportedModel);
        lock (syncRoot)
        {
            modelRegistry[supportedModel] = typeof(TConfig);
        }
    }

    public static IDeviceConfig CreateConfigFor(string deviceModelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceModelName);
        EnsureLoadedAssembliesScanned();

        lock (syncRoot)
        {
            if (modelRegistry.TryGetValue(deviceModelName, out var type))
            {
                return (IDeviceConfig)Activator.CreateInstance(type)!;
            }
        }

        throw new InvalidOperationException($"未找到型号 {deviceModelName} 的配置类。");
    }

    private static void EnsureLoadedAssembliesScanned()
    {
        lock (syncRoot)
        {
            if (loadedAssembliesScanned)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterConfigsFromAssemblyCore(assembly);
            }

            loadedAssembliesScanned = true;
        }
    }

    private static void RegisterConfigsFromAssemblyCore(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (!typeof(IDeviceConfig).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not IDeviceConfig instance)
            {
                continue;
            }

            var modelProp = type.GetProperty("SupportedModel", BindingFlags.Public | BindingFlags.Instance);
            if (modelProp?.GetValue(instance) is string modelName && !string.IsNullOrWhiteSpace(modelName))
            {
                modelRegistry[modelName] = type;
            }
        }
    }
}
