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

    internal static VisaRuntimeUnavailableException CreateUnavailableException(
        string? resourceName,
        Exception innerException)
        => new(CreateUnavailableMessage(resourceName), resourceName, innerException);

    private static string CreateUnavailableMessage(string? resourceName = null)
    {
        string resource = string.IsNullOrWhiteSpace(resourceName)
            ? string.Empty
            : $" 无法打开资源 '{resourceName}'。";

        return "未检测到可用的 VISA Runtime。" + resource
            + "请安装与应用程序位数兼容的 NI-VISA Runtime；使用 NI GPIB 板卡或适配器时还需安装 NI-488.2 驱动。";
    }
}

