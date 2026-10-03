using NationalInstruments.Visa;

namespace Kwy.Communicate.Visa;

/// <summary>发现本机 NI-VISA Runtime 提供的 VISA 资源。</summary>
public static class VisaResourceDiscovery
{
    /// <summary>
    /// 检测指定资源是否在当前计算机上可用。未安装运行时、缺少接口驱动或资源不存在时均不抛异常。
    /// </summary>
    public static VisaResourceAvailabilityResult CheckAvailability(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        resourceName = resourceName.Trim();

        VisaRuntimeStatus runtime = VisaRuntime.CheckAvailability();
        if (!runtime.IsAvailable)
        {
            return new VisaResourceAvailabilityResult(
                VisaResourceAvailability.RuntimeUnavailable,
                resourceName,
                runtime.Message,
                runtime.Exception);
        }

        try
        {
            using var resourceManager = new ResourceManager();
            bool found = resourceManager.Find(resourceName)
                .Any(item => string.Equals(item, resourceName, StringComparison.OrdinalIgnoreCase));
            return found
                ? new VisaResourceAvailabilityResult(
                    VisaResourceAvailability.Available,
                    resourceName,
                    $"已发现 VISA 资源 '{resourceName}'。")
                : new VisaResourceAvailabilityResult(
                    VisaResourceAvailability.ResourceNotFound,
                    resourceName,
                    VisaRuntime.CreateResourceNotFoundException(resourceName).Message);
        }
        catch (Exception ex) when (VisaRuntime.IsRuntimeUnavailable(ex))
        {
            VisaInterfaceUnavailableException unavailable =
                VisaRuntime.CreateInterfaceUnavailableException(resourceName, ex);
            return new VisaResourceAvailabilityResult(
                VisaResourceAvailability.InterfaceUnavailable,
                resourceName,
                unavailable.Message,
                ex);
        }
        catch (Exception ex) when (VisaRuntime.IsResourceNotFound(ex))
        {
            return new VisaResourceAvailabilityResult(
                VisaResourceAvailability.ResourceNotFound,
                resourceName,
                VisaRuntime.CreateResourceNotFoundException(resourceName, ex).Message,
                ex);
        }
    }

    /// <summary>返回与 VISA 查询表达式匹配的资源名称，例如 <c>?*::INSTR</c>。</summary>
    public static IReadOnlyList<string> Find(string expression = "?*::INSTR")
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NationalInstruments.Visa requires Windows.");
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        try
        {
            using var resourceManager = new ResourceManager();
            return resourceManager.Find(expression).Order(StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (VisaRuntime.IsResourceNotFound(ex))
        {
            // VISA 规范把“没有匹配资源”表示为 VI_ERROR_RSRC_NFOUND。
            // 对资源发现 API 而言，这应当是空集合，而不是需要调用方捕获的异常。
            return Array.Empty<string>();
        }
        catch (Exception ex) when (VisaRuntime.IsRuntimeUnavailable(ex))
        {
            throw VisaRuntime.CreateUnavailableException(resourceName: null, ex);
        }
    }
}
