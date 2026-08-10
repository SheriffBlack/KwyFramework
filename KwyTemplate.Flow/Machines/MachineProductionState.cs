namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 生产动作状态。
/// Runtime 线程随程序启动常驻运行；这里仅表示开始、暂停、停止按钮形成的生产业务状态。
/// </summary>
public enum MachineProductionState
{
    Stopped,
    Running,
    Paused
}