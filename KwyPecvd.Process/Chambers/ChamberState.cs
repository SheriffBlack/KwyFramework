namespace KwyPecvd.Process.Chambers;

public enum ChamberState
{
    /// <summary>腔体尚未初始化，或依赖硬件不可用</summary>
    Offline,

    /// <summary>检查MFC、阀门等硬件绑定</summary>
    Initializing,

    /// <summary>初始化完成，没有工艺运行</summary>
    Idle,

    /// <summary>工艺启动前准备，如抽真空、加热、气体稳定</summary>
    Preparing,

    /// <summary>正在执行工艺</summary>
    Processing,

    /// <summary>正在安全停止输出</summary>
    Stopping,

    /// <summary>模块发生故障</summary>
    Faulted
}