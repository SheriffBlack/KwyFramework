namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 仪器和 GPIB 设备使用的文本命令与查询功能。
/// </summary>
public interface ITextCommandClient : IProtocolClient
{
    /// <summary>写入一条不需要返回响应的文本命令。</summary>
    ValueTask WriteAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>写入查询命令并返回完整的文本响应。</summary>
    ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default);
}
