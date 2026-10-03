namespace Kwy.Communicate.Visa;

/// <summary>因计算机未安装或无法加载 VISA 本机运行时而引发的异常。</summary>
public sealed class VisaRuntimeUnavailableException : InvalidOperationException
{
    /// <summary>初始化异常。</summary>
    public VisaRuntimeUnavailableException(
        string message,
        string? resourceName = null,
        Exception? innerException = null)
        : base(message, innerException)
        => ResourceName = resourceName;

    /// <summary>尝试访问的 VISA 资源名称；仅检测运行时时可能为 <see langword="null"/>。</summary>
    public string? ResourceName { get; }
}

