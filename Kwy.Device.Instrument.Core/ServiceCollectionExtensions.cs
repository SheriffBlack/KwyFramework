using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.Instrument.Core;

/// <summary>
/// 注册仪表领域的共享基础能力。
/// <para>具体仪表由厂商适配包注册；本方法只建立仪表运行所依赖的设备基础设施。</para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册仪表领域 Core。
    /// </summary>
    public static IServiceCollection AddInstrumentCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();
        return services;
    }
}
