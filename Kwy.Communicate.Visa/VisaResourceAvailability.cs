namespace Kwy.Communicate.Visa;

/// <summary>VISA 资源在当前计算机上的可用状态。</summary>
public enum VisaResourceAvailability
{
    /// <summary>资源可用。</summary>
    Available,

    /// <summary>未安装或无法加载 VISA Runtime。</summary>
    RuntimeUnavailable,

    /// <summary>VISA Runtime 可用，但相应的 GPIB 等接口驱动不可用。</summary>
    InterfaceUnavailable,

    /// <summary>接口可用，但未发现指定资源。</summary>
    ResourceNotFound
}

/// <summary>VISA 资源检测结果。</summary>
public sealed record VisaResourceAvailabilityResult(
    VisaResourceAvailability Availability,
    string ResourceName,
    string Message,
    Exception? Exception = null)
{
    /// <summary>指定资源是否可用。</summary>
    public bool IsAvailable => Availability == VisaResourceAvailability.Available;
}

