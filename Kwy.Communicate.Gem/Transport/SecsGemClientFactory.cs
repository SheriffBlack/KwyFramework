using Microsoft.Extensions.Options;
using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 提供不依赖依赖注入容器的 SECS/GEM 客户端创建入口。
/// </summary>
public static class SecsGemClientFactory
{
    /// <summary>
    /// 根据指定配置创建 SECS/GEM 客户端，并将诊断信息输出到
    /// <see cref="System.Diagnostics.Trace"/>。
    /// </summary>
    /// <param name="config">SECS/GEM 客户端配置。</param>
    /// <returns>由调用方负责释放的 SECS/GEM 客户端。</returns>
    /// <exception cref="ArgumentException">配置无效。</exception>
    /// <remarks>适合学习、控制台工具和临时联调；生产项目应显式传入 <see cref="IGemDiagnostics"/>。</remarks>
    public static ISecsGemClient Create(SecsGemClientConfig config)
        => Create(config, new TraceGemDiagnostics());

    /// <summary>
    /// 根据指定配置和业务诊断接收器创建 SECS/GEM 客户端。
    /// </summary>
    /// <param name="config">SECS/GEM 客户端配置。</param>
    /// <param name="diagnostics">GEM 诊断接收器。</param>
    /// <param name="diagnosticsOptions">诊断选项；为空时使用安全的默认值。</param>
    /// <returns>由调用方负责释放的 SECS/GEM 客户端。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> 或 <paramref name="diagnostics"/> 为空。</exception>
    /// <exception cref="ArgumentException">配置无效。</exception>
    public static ISecsGemClient Create(
        SecsGemClientConfig config,
        IGemDiagnostics diagnostics,
        GemDiagnosticsOptions? diagnosticsOptions = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(diagnostics);
        SecsGemClientConfig clientConfig = SecsGemClient.CloneAndValidateConfig(config);

        var options = Options.Create(CreateOptions(clientConfig));
        diagnosticsOptions ??= new GemDiagnosticsOptions();
        diagnosticsOptions.Validate();
        var logger = new SecsGemLoggerAdapter(diagnostics, diagnosticsOptions);

        return CreateClient(clientConfig, options, logger);
    }

    internal static ISecsGemClient CreateWithLogger(
        SecsGemClientConfig config,
        ISecsGemLogger logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        SecsGemClientConfig clientConfig = SecsGemClient.CloneAndValidateConfig(config);

        return CreateClient(clientConfig, Options.Create(CreateOptions(clientConfig)), logger);
    }

    private static ISecsGemClient CreateClient(
        SecsGemClientConfig config,
        IOptions<SecsGemOptions> options,
        ISecsGemLogger logger)
    {
        var connection = new HsmsConnection(options, logger)
        {
            LinkTestEnabled = config.KeepAlive
        };
        var secsGem = new SecsGem(options, connection, logger);

        return new SecsGemClient(secsGem, connection, config, ownsSecs4NetDependencies: true);
    }

    internal static SecsGemOptions CreateOptions(SecsGemClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new SecsGemOptions
        {
            DeviceId = config.DeviceId,
            IsActive = config.IsActive,
            IpAddress = config.Host,
            Port = config.Port,
            T3 = config.T3Timeout,
            T5 = config.T5Timeout,
            T6 = config.T6Timeout,
            T7 = config.T7Timeout,
            T8 = config.T8Timeout,
            LinkTestInterval = config.KeepAliveInterval
        };
    }
}
