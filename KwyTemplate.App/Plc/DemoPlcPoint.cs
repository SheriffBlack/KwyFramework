using Kwy.ComponentModel;
using System.ComponentModel;

namespace KwyTemplate.App.Plc;

public enum DemoPlcPoint
{
    [Description("不良品盒锁1手动操作")]
    [PlcPoint("R100", typeof(bool))]
    BadProductBoxLock1Manual,

    [Description("不良品盒锁2手动操作")]
    [PlcPoint("R101", typeof(bool))]
    BadProductBoxLock2Manual,

    [Description("不良品盒锁3手动操作")]
    [PlcPoint("R102", typeof(bool))]
    BadProductBoxLock3Manual,

    [Description("不良品盒锁4手动操作")]
    [PlcPoint("R103", typeof(bool))]
    BadProductBoxLock4Manual,

    [Description("不良品盒锁5手动操作")]
    [PlcPoint("R104", typeof(bool))]
    BadProductBoxLock5Manual,

    [Description("易损件次数到达报警")]
    [PlcPoint("R130", typeof(bool), IsReadOnly = true)]
    WearingPartCountReachedAlarm,

    [Description("气压检测报警")]
    [PlcPoint("R131", typeof(bool), IsReadOnly = true)]
    AirPressureDetectionAlarm,

    [Description("当前数量")]
    [PlcPoint("DM100", typeof(int))]
    CurrentQuantity,

    [Description("设置数量")]
    [PlcPoint("DM102", typeof(int))]
    SetQuantity
}
