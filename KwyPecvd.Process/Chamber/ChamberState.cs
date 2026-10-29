namespace KwyPecvd.Process.Chamber;

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
                   初始化命令
Offline ──────────────────────────→ Initializing
                                         │
                             硬件检查成功│
                                         ▼
                                        Idle
                                         │
                         接受准备命令    │
                                         ▼
                                     Preparing
                                         │
                Gas/Vacuum Actual全部满足│
                                         ▼
                                     Processing
                                         │
                          接受停止命令   │
                                         ▼
                                      Stopping
                                         │
                     Actual确认安全停止  │
                                         ▼
                                        Idle

任意关键故障 ───────────────────────────→ Faulted


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