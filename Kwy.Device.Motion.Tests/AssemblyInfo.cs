using Xunit;

// 运动卡、状态监视器与模拟 IO 的测试包含共享的异步硬件时序，不应以并行调度制造伪随机超时。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
