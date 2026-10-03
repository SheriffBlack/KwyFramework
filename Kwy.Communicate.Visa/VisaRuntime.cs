using Ivi.Visa;
using NationalInstruments.Visa;

namespace Kwy.Communicate.Visa;

/// <summary>VISA 本机运行时的可用状态。</summary>
public sealed record VisaRuntimeStatus
{
    internal VisaRuntimeStatus(bool isAvailable, string message, Exception? exception = null)
    {
        IsAvailable = isAvailable;
        Message = message;
        Exception = exception;
    }

    /// <summary>当前进程是否能够加载 VISA 本机运行时。</summary>
    public bool IsAvailable { get; }

    /// <summary>面向用户的状态说明。</summary>
    public string Message { get; }

    /// <summary>检测失败时的原始异常；运行时可用时为 <see langword="null"/>。</summary>
    public Exception? Exception { get; }
}

/// <summary>检测当前计算机是否具备 VISA 通信所需的本机运行时。</summary>
public static class VisaRuntime
{
    /// <summary>
    /// 检测 VISA 本机运行时。该方法不会因为未安装运行时而抛出异常，适合 UI 在启用连接功能前调用。
    /// </summary>
    public static VisaRuntimeStatus CheckAvailability()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new VisaRuntimeStatus(
                false,
                "当前系统不受支持；NationalInstruments.Visa 需要 Windows。");
        }

        try
        {
            using var resourceManager = new ResourceManager();
            _ = resourceManager.Find("?*");
            return new VisaRuntimeStatus(true, "已检测到可用的 VISA Runtime。");
        }
        catch (Exception ex) when (IsResourceNotFound(ex))
        {
            // VISA 本机实现已成功加载，只是当前没有可枚举的资源。
            // 这是正常的空结果，不能误判为 Runtime 缺失。
            return new VisaRuntimeStatus(true, "已检测到可用的 VISA Runtime，但当前未发现 VISA 资源。");
        }
        catch (Exception ex) when (IsRuntimeUnavailable(ex))
        {
            return new VisaRuntimeStatus(false, CreateUnavailableMessage(), ex);
        }
    }

    internal static bool IsRuntimeUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NativeVisaException visaException
                && visaException.ErrorCode == NativeErrorCode.LibraryNotFound)
            {
                return true;
            }

            if (current is DllNotFoundException or BadImageFormatException)
                return true;
        }

        return false;
    }

    internal static bool IsResourceNotFound(Exception exception)
        => exception is NativeVisaException visaException
            && visaException.ErrorCode == NativeErrorCode.ResourceNotFound;

    internal static VisaInterfaceUnavailableException CreateInterfaceUnavailableException(
        string resourceName,
        Exception innerException)
        => new(
            $"已检测到 VISA Runtime，但无法加载资源 '{resourceName}' 所需的接口驱动。"
            + (resourceName.StartsWith("GPIB", StringComparison.OrdinalIgnoreCase)
                ? "请确认 GPIB 控制器及其驱动已安装，并已在 NI MAX 或对应厂商工具中识别。"
                : string.Empty),
            resourceName,
            innerException);

    internal static VisaResourceNotFoundException CreateResourceNotFoundException(
        string resourceName,
        Exception? innerException = null)
        => new(
            $"未发现 VISA 资源 '{resourceName}'。资源名称只是配置，不代表本机已存在对应接口或仪器。",
            resourceName,
            innerException);

    internal static VisaRuntimeUnavailableException CreateUnavailableException(
        string? resourceName,
        Exception innerException)
        => new(CreateUnavailableMessage(resourceName), resourceName, innerException);

    private static string CreateUnavailableMessage(string? resourceName = null)
    {
        if (!string.IsNullOrWhiteSpace(resourceName)
            && resourceName.StartsWith("GPIB", StringComparison.OrdinalIgnoreCase))
        {
            return $"无法加载 GPIB 本机驱动，因此无法打开资源 '{resourceName}'。"
                + "仅安装 NI-VISA 不足以驱动 NI GPIB 板卡或 USB-GPIB 适配器；"
                + "请安装 NI-488.2，并确认设备已在 NI MAX 或 Windows 设备管理器中正常识别。";
        }

        string resource = string.IsNullOrWhiteSpace(resourceName)
            ? string.Empty
            : $" 无法打开资源 '{resourceName}'。";

        return "无法加载 VISA 本机实现或对应资源类型的接口驱动。" + resource
            + "请安装与应用程序位数兼容的 VISA Runtime 和相应硬件驱动。";
    }
}
