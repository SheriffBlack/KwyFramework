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

/// <summary>一条“可反复运行”的正常业务链路</summary>

/*
初始化
Offline → Initializing → Idle

启动气体准备
Idle → Preparing

MFC执行
目标流量 → SetPoint斜坡 → Feedback跟随

准备完成
Preparing → Processing

停止工艺
Processing → Stopping

安全归零
SetPoint归零 → Feedback进入零点容差

停止完成
Stopping → Idle

完整调用链:
    外部命令
        → ChamberController
        → ChamberGasSystem
        → IMfc
        → 具体MFC实现
        → RT硬件周期
        → MfcSnapshot
        → RT模块周期
        → Chamber状态转换
        → ChamberSnapshot/诊断接口
*/