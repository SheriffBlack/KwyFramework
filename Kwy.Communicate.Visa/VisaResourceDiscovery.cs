using NationalInstruments.Visa;

namespace Kwy.Communicate.Visa;

/// <summary>发现本机 NI-VISA Runtime 提供的 VISA 资源。</summary>
public static class VisaResourceDiscovery
{
    /// <summary>返回与 VISA 查询表达式匹配的资源名称，例如 <c>?*::INSTR</c>。</summary>
    public static IReadOnlyList<string> Find(string expression = "?*::INSTR")
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NationalInstruments.Visa requires Windows.");
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        using var resourceManager = new ResourceManager();
        return resourceManager.Find(expression).Order(StringComparer.Ordinal).ToArray();
    }
}
