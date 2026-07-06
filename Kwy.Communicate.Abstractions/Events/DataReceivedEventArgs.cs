using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kwy.Communicate.Abstractions.Events;

/// <summary>
/// 数据接收事件参数
/// </summary>
public class DataReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 接收到的数据
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public int Length { get; }

    public DataReceivedEventArgs(byte[] data, int length)
    {
        Data = data;
        Length = length;
    }
}
