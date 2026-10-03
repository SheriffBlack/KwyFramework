namespace Kwy.Communicate.Visa;

/// <summary>VISA Runtime 已加载，但指定资源所需的接口驱动不可用。</summary>
public sealed class VisaInterfaceUnavailableException : InvalidOperationException
{
    /// <summary>初始化异常。</summary>
    public VisaInterfaceUnavailableException(string message, string resourceName, Exception? innerException = null)
        : base(message, innerException)
        => ResourceName = resourceName;

    /// <summary>尝试访问的 VISA 资源名称。</summary>
    public string ResourceName { get; }
}

