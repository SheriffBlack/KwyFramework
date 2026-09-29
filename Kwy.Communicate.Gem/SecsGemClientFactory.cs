using Microsoft.Extensions.Options;
using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 提供不依赖依赖注入容器的 SECS/GEM 客户端创建入口。
/// </summary>
public static class SecsGemClientFactory
{
    /// <summary>
    /// 根据指定配置创建 SECS/GEM 客户端。
    /// </summary>
    /// <param name="config">SECS/GEM 客户端配置。</param>
    /// <param name="diagnostics">GEM 诊断接收器；为空时输出到 <see cref="System.Diagnostics.Trace"/>。</param>
    /// <param name="diagnosticsOptions">诊断选项；为空时使用安全的默认值。</param>
    /// <returns>由调用方负责释放的 SECS/GEM 客户端。</returns>
    /// <exception cref="ArgumentException">配置无效。</exception>
    public static ISecsGemClient Create(
        SecsGemClientConfig config,
        IGemDiagnostics? diagnostics = null,
        GemDiagnosticsOptions? diagnosticsOptions = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        Validate(config);

        var options = Options.Create(CreateOptions(config));
        diagnostics ??= new TraceGemDiagnostics();
        diagnosticsOptions ??= new GemDiagnosticsOptions();
        diagnosticsOptions.Validate();
        var logger = new SecsGemLoggerAdapter(diagnostics, diagnosticsOptions);

        return CreateClient(config, options, logger);
    }

    /// <summary>
    /// 使用自定义 Secs4Net 日志记录器创建客户端，供需要直接控制第三方接口的高级场景使用。
    /// </summary>
    public static ISecsGemClient CreateWithLogger(
        SecsGemClientConfig config,
        ISecsGemLogger logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        Validate(config);

        return CreateClient(config, Options.Create(CreateOptions(config)), logger);
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

    internal static void Validate(SecsGemClientConfig config)
    {
        if (!config.Validate())
        {
            throw new ArgumentException("SECS/HSMS 配置无效。", nameof(config));
        }
    }
}
