namespace Kwy.Communicate.Visa;

/// <summary>VISA 运行环境可用，但未发现指定资源。</summary>
public sealed class VisaResourceNotFoundException : InvalidOperationException
{
    /// <summary>初始化异常。</summary>
    public VisaResourceNotFoundException(string message, string resourceName, Exception? innerException = null)
        : base(message, innerException)
        => ResourceName = resourceName;

    /// <summary>未发现的 VISA 资源名称。</summary>
    public string ResourceName { get; }
}

