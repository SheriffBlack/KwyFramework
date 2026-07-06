using Kwy.Device.Abstractions;
using System.Reflection;

namespace Kwy.Device.Core.Instrument;

public class ConfigFactory
{
    // 建立一张高速缓存映射表：<"GOM804", typeof(GOM804ResistorConfig)>
    private static readonly Dictionary<string, Type> _modelRegistry = new(StringComparer.OrdinalIgnoreCase);

    // 静态构造函数：程序第一次用到这个类时自动执行，只执行一次
    static ConfigFactory()
    {
        var configTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IDeviceConfig).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in configTypes)
        {
            // 因为这些 Config 都是轻量级的数据壳子，实例化一次几乎不耗性能
            var instance = (IDeviceConfig)Activator.CreateInstance(type)!;

            // 通用：只要这个 Config 类有 SupportedModel 属性（无论继承自哪个基类）就注册
            var modelProp = type.GetProperty("SupportedModel");
            if (modelProp != null)
            {
                var modelName = modelProp.GetValue(instance) as string;
                if (!string.IsNullOrEmpty(modelName))
                {
                    _modelRegistry[modelName] = type;
                }
            }
        }
    }

    // 以后每次要工厂造东西，直接去字典里拿图纸，速度起飞！
    public static IDeviceConfig CreateConfigFor(string deviceModelName)
    {
        if (_modelRegistry.TryGetValue(deviceModelName, out var type))
        {
            return (IDeviceConfig)Activator.CreateInstance(type)!;
        }
        throw new Exception($"未找到型号 {deviceModelName} 的配置类！");
    }
}
