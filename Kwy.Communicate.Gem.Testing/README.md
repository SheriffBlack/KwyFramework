# Kwy.Communicate.Gem.Testing

`InMemoryGemProcessProgramRepository` 用于 Stream 7 测试和 Host Simulator。它不持久化数据，
不应在生产设备中代替 Recipe Service。

`Kwy.Communicate.Gem.Testing` 是独立于设备生产运行时的 Host 侧 GEM 接口验收工具包。
它把 HSMS 连接、GEM 通信建立、Online、S1F3、S2F41、S6F11、S5F1 和 Stream 7 组合为
可脚本化步骤，输出 JSON/Markdown 结果。

> 它是自动化冒烟与回归验收基础，不等于 SEMI 认证，也不代替客户 Host Simulator、
> SEDD 逐项对接和设备安全验收。

```csharp
await using ISecsGemClient client = SecsGemClientFactory.Create(hostConfig);
await using var transport = new SecsGemHostTransport(client, ownsClient: false);

var plan = new GemAcceptancePlan("AOI-01 smoke")
    .Add(GemAcceptanceSteps.Connect())
    .Add(GemAcceptanceSteps.EstablishCommunication("KwyHost", "1.0"))
    .Add(GemAcceptanceSteps.RequestOnline())
    .Add(GemAcceptanceSteps.ReadStatusVariables([new GemVid(1001)]))
    .Add(GemAcceptanceSteps.ReadProcessProgramDirectory())
    .Add(GemAcceptanceSteps.Disconnect());

var context = new GemAcceptanceContext(transport, TimeSpan.FromSeconds(45));
GemAcceptanceReport report = await plan.RunAsync(context);
await File.WriteAllTextAsync("gem-acceptance.json", report.ToJson());
await File.WriteAllTextAsync("gem-acceptance.md", report.ToMarkdown());
```

生产验收建议为每个客户保存一份 C# Plan 或由项目层把受控 JSON 映射为 Plan；报文结构校验和
设备动作触发仍保留在编译后的 C# 中。`S2F42 HCACK=0` 只表示命令已受理，还要通过
Collection Event/状态变量验证真正完成。
