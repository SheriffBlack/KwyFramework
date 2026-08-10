# 标准件、确认件与点检补偿界面设计

本文说明 `KwyTemplate.App` 中标准件/确认件查询结果如何流向点检补偿界面，以及为什么采用共享状态模型来连接 `StandardView` 和 `CompensateView`。

## 背景

点检页面由两部分组成：

- `StandardView`：负责输入标准件、确认件编号，调用 MES 查询接口，并解析本地 MES 文件中的上下限、标准值等参数。
- `CompensateView`：负责按当前机台需要点检的工站展示点检流程，并显示标准件/确认件对应的上限、下限、测试值。

两者在界面上是不同 View，也有各自的 ViewModel。如果让 `CompensateViewModel` 直接访问 `StandardViewModel`，会让两个界面互相依赖，后续页面缓存、导航生命周期、单元测试都会变复杂。因此这里不让 ViewModel 互相引用，而是抽出一个应用内共享状态。

## 核心设计

当前设计引入 `StandardSampleState`：

```csharp
public sealed class StandardSampleState
{
    public StandardSamplePanelModel StandardSample { get; } = new("标准件");

    public StandardSamplePanelModel ConfirmSample { get; } = new("确认件");
}
```

该对象在 `AppModule` 中注册为单例：

```csharp
containerRegistry.AddSingleton<StandardSampleState>();
```

这样：

- `StandardViewModel` 查询 MES 后，把标准件/确认件结果写入同一个 `StandardSampleState`。
- `CompensateViewModel` 读取同一个 `StandardSampleState`，并把其中的上下限映射到工站点检项。
- 两个 ViewModel 不需要互相知道对方，也不依赖具体 View 是否已经显示。

数据流如下：

```text
MES 查询 / 文件解析
        ↓
StandardViewModel
        ↓
StandardSampleState
        ↓
CompensateViewModel
        ↓
CompensateView ItemsControl
```

## StandardView 的职责

`StandardView` 只关心标准件、确认件本身：

- 输入标准件编号或确认件编号。
- 检查 Home 中的工单、机台号是否存在。
- 调用 `IMesStandardSampleService.GetStandardSampleAsync(...)`。
- 解析 MES 返回后的本地文件。
- 将结果填入 `StandardSamplePanelModel.LimitItems`。

`LimitItems` 中每一项代表一个仪表项目，例如：

```text
DCR
LCR
RS
```

如果文件 `标准件.txt` 只有一行 DCR，则只生成一个 `LimitItem`。

如果文件 `标准件_3.txt` 有三行 DCR/LCR/RS，则会生成三个 `LimitItem`。

## CompensateView 的职责

`CompensateView` 不直接按 MES 文件生成 UI，而是按当前 Machine 中标记了点检的工站生成 UI。

来源是：

```csharp
machine.TestStations.Where(HasStationCheckOperation)
```

也就是工站中声明了：

```csharp
new StationOperationDescriptor
{
    Code = StationOperationDescriptor.Check,
    DisplayName = "点检"
}
```

才会进入 `CheckItems`。

这样做的原因是：

- UI 展示的是当前机台实际要点检的工站。
- 不同机型可以有不同的点检工站数量。
- MES 文件只提供参数，不决定机台有哪些点检工站。

## 点检流程集合与工站集合

`CompensateView` 中有两类 `ItemsControl`，含义不同，不能共用同一个集合。

第一类是点检流程步骤集合，绑定 `CheckFlowItems`：

```text
标准件
确认件
```

这表示一次点检流程需要经历哪些阶段。当前只有标准件和确认件，后续如果业务增加其他阶段，可以继续往 `CheckFlowItems` 中添加。

第二类是机台点检工站集合，绑定 `CheckItems`。它来自当前 Machine 中标记了 `StationOperationDescriptor.Check` 的工站，例如：

```text
DCR1
DCR2
```

因此命名上：

- `CheckFlowItems`：点检流程步骤，属于 App 页面流程状态。
- `CheckItems`：需要点检的机台工站，来源于 Machine 定义。

不要把 `CheckItems` 改名为标准件/确认件集合，因为标准件区域、确认件区域下方的参数展示仍然是按工站展开的。

## CheckItems 与 LimitItems 的映射

`CompensateView` 里的标准件/确认件区域使用 `ItemsControl` 绑定 `CheckItems`。

每个 `CheckItem` 显示：

```text
工站名
上限
下限
测试值
```

其中工站名来自 `StationCheckItemModel.DisplayName`，上下限和标准值来自共享的 `StandardSampleState`。

映射规则在 `CompensateViewModel.ApplySampleLimitReferences()` 中完成：

```csharp
for (int i = 0; i < CheckItems.Count; i++)
{
    StationCheckItemModel item = CheckItems[i];
    item.SetStandardLimitItem(GetLimitItem(sampleState.StandardSample.LimitItems, i));
    item.SetConfirmLimitItem(GetLimitItem(sampleState.ConfirmSample.LimitItems, i));
}
```

`GetLimitItem(...)` 的规则是：

- 如果 `LimitItems` 为空，则该工站显示空值。
- 如果 `LimitItems` 数量足够，则按相同索引取值。
- 如果 `CheckItems` 比 `LimitItems` 多，则复用最后一个 `LimitItem`。

```csharp
private static StandardSampleLimitItemModel? GetLimitItem(IList<StandardSampleLimitItemModel> limitItems, int index)
{
    if (limitItems.Count == 0)
    {
        return null;
    }

    return limitItems[Math.Min(index, limitItems.Count - 1)];
}
```

## 为什么数量不一致时复用最后一项

现场会出现这种情况：

- Machine 中有两个 DCR 点检工站，例如 `DCR1`、`DCR2`。
- MES 标准件文件中只有一项 DCR 参数。

这并不表示数据错误，而是两个 DCR 工站共享同一组标准件上下限。

因此：

```text
CheckItems:  DCR1, DCR2
LimitItems:  DCR
```

映射结果为：

```text
DCR1 → DCR 上下限
DCR2 → DCR 上下限
```

如果后续某个机型有 DCR/LCR/RS 三个不同项目，并且 MES 文件也提供三项，则自然按索引一一对应。

## UI 绑定方式

`CompensateView` 中标准件区域的 TextBox 绑定：

```xml
<TextBox Text="{Binding StandardUpperLimit, Mode=OneWay}" />
<TextBox Text="{Binding StandardLowerLimit, Mode=OneWay}" />
<TextBox Text="{Binding StandardValue, Mode=OneWay}" />
```

确认件区域的 TextBox 绑定：

```xml
<TextBox Text="{Binding ConfirmUpperLimit, Mode=OneWay}" />
<TextBox Text="{Binding ConfirmLowerLimit, Mode=OneWay}" />
<TextBox Text="{Binding ConfirmValue, Mode=OneWay}" />
```

这些属性都在 `StationCheckItemModel` 中提供。`StationCheckItemModel` 不复制上下限文本，而是引用对应的 `StandardSampleLimitItemModel`。

因为 `TextBox.Text` 默认是 `TwoWay`，而这些显示属性是只读属性，所以这里必须显式设置 `Mode=OneWay`。

这样当 `StandardView` 查询完成、`LimitItem` 的 `UpperLimit`、`LowerLimit`、`StandardValue` 被更新时，`CompensateView` 会收到属性变更通知并刷新。

## 为什么不直接绑定 StandardSampleState.LimitItems

`CompensateView` 的 UI 数量应该由机台工站决定，而不是由 MES 文件决定。

如果直接绑定 `StandardSampleState.StandardSample.LimitItems`，会出现几个问题：

- MES 文件只有 DCR 一项时，界面只显示一列，但实际机台可能有 DCR1、DCR2 两个点检工站。
- MES 文件有 DCR/LCR/RS 三项时，界面会显示三列，但当前机台未必都有这三个工站。
- 后续换机型时，UI 会跟 MES 文件耦合，而不是跟 Machine 定义耦合。

所以 `CompensateView` 仍然以 `CheckItems` 为主，`LimitItems` 只作为每个 `CheckItem` 的参数来源。

## 当前边界

当前实现只解决标准件/确认件上下限和标准值的展示。

以下内容暂未纳入本文设计：

- 点检测试值由仪表实时回写。
- 点检结果 OK/NG 判定。
- 标准件点检保存 MES。
- 确认件点检保存 MES。
- 多仪表项目按 Code 精确匹配，而不是按索引映射。

其中“按 Code 精确匹配”后续可以增强。例如 `DCR1`、`DCR2` 都归一化为 `DCR`，优先匹配 `LimitItem.Code == DCR`。当前先采用索引加复用最后一项，满足现阶段 Machine_2_R 的两个 DCR 工站共享一组 DCR 参数的需求。

## 维护建议

后续新增机型时，应遵循以下约定：

1. 在 Machine 的 `InitTestStations()` 中声明需要点检的工站。
2. 工站需要包含 `StationOperationDescriptor.Check`。
3. `StandardView` 继续负责 MES 查询和标准件/确认件解析。
4. `CompensateView` 继续以 `CheckItems` 生成工站参数 UI。
5. 如果某机型需要更复杂的映射规则，优先扩展 `CompensateViewModel` 的映射逻辑，不要让 View 直接处理业务判断。

这个设计的核心是：

```text
Machine 决定界面要显示哪些点检项；MES 决定这些点检项使用哪些参数。
```
## 自动点检时间窗口设计

自动点检用于按班次时间要求提醒操作员完成点检。它不是 Machine 的底层能力，而是 App 层业务编排能力，因为它同时依赖点检配置、点检界面流程状态、MES 保存结果和弹窗提示。

推荐放在 `KwyTemplate.App.Orchestration` 中实现，例如新增 `CompensateScheduleMonitorFeature`，并接入现有 `MachineRuntimeOrchestrator`。这样可以和 Reel 扫码、PLC 停机信号等常驻业务保持同一套生命周期：程序启动时启动，程序退出时停止。

### 配置来源

时间窗口来自 `CompensateOptionsStore.Current`：

```text
IsEnabled           是否启用自动点检提醒
CompensateATime1    A 班时间 1
CompensateATime2    A 班时间 2
CompensateBTime1    B 班时间 1
CompensateBTime2    B 班时间 2
```

每个时间点表示一个 2 小时点检窗口。例如 `CompensateATime1 = 8` 表示：

```text
08:00:00 ~ 10:00:00
```

如果点检在这个窗口内完成，则该窗口通过；如果窗口结束后仍未完成，则弹窗提示：

```text
未在规定时间 8~10 内点检，请完成！
```

B 班时间可能跨天，例如 `23` 对应 `23:00 ~ 次日 01:00`，因此不能只比较小时数字，必须用完整的 `DateTimeOffset` 计算窗口开始和结束时间。

### 点检完成时间

点检完成时间不能简单等同于 UI 上 CheckBox 被勾选，也不能只看某个仪表测试结束。

准确完成点应该是：

1. `CompensateViewModel.CheckFlowItems` 中所有流程项都已经完成。
2. 最后一项保存 MES 成功。
3. `MesCyntecService.SaveStandardSampleCheckAsync(...)` 返回 `ReturnCode == 0`。

也就是 MES 保存成功的一瞬间，才发布“本次点检流程完成”消息。

推荐新增消息：

```csharp
public sealed record CompensateWorkflowCompletedMessage(
    DateTimeOffset CompletedAt,
    string? WorkOrderNo,
    string? EquipmentNo);
```

由 `CompensateViewModel` 在确认全部流程完成后发布：

```text
CompensateViewModel
  -> SaveStandardSampleCheckAsync 成功
  -> CheckFlowItems 全部完成
  -> IMessageBus.Publish(CompensateWorkflowCompletedMessage)
```

`CompensateScheduleMonitorFeature` 订阅该消息。收到消息后，如果 `CompletedAt` 落在当前点检窗口内，则把该窗口标记为已完成。

### 定时器防漂移

这里不建议使用“启动后 Delay 到下一个时间点，再 Delay 两小时”的累计等待方式。程序 7x24 小时运行时，线程调度、系统休眠、GC 暂停、弹窗阻塞、异常恢复等都会让累计等待产生偏移。

正确做法是：定时器只负责唤醒检查，真实时间永远来自系统当前时间。

推荐实现方式：

```text
PeriodicTimer 每 30 秒或 60 秒唤醒一次
  -> 读取 DateTimeOffset.Now
  -> 根据当前日期和 CompensateOptions 重新计算当天/跨天窗口
  -> 判断当前窗口是否开始、是否结束、是否已完成、是否已提醒
```

这样即使某次轮询延迟了，下一次也会用当前真实时间重新判断，不会把误差继续累加下去。

点检提醒属于小时级业务，不需要 1ms 或 10ms 轮询。推荐 30 秒或 60 秒一次，既足够准确，也不会给 UI 线程和后台线程带来压力。

### 窗口状态

每个窗口运行时只需要维护轻量状态：

```csharp
public sealed class CompensateScheduleWindowState
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public bool IsCompleted { get; set; }
    public bool WarningShown { get; set; }
}
```

判断规则：

```text
收到点检完成消息：
  如果 CompletedAt 在窗口 Start ~ End 内，IsCompleted = true

周期检查：
  如果 now > End，并且 IsCompleted == false，并且 WarningShown == false
    弹窗提示未按时点检
    WarningShown = true
```

`WarningShown` 用于保证同一个窗口只弹一次，避免长时间未点检时反复弹窗。

### 配置变更

`SetView` 中保存 `CompensateOptions` 后，建议通过 `IMessageBus` 发布配置变更消息：

```csharp
public sealed record CompensateOptionsChangedMessage(CompensateOptions Options);
```

`CompensateScheduleMonitorFeature` 收到后重新生成时间窗口。这样现场修改点检时间后不需要重启程序。

### 架构边界

该功能的边界建议如下：

```text
CompensateOptionsStore
  负责点检配置持久化和读取

CompensateViewModel
  负责点检流程、MES 保存，并在全部完成后发布完成消息

CompensateScheduleMonitorFeature
  负责按配置监控时间窗口，订阅点检完成消息，到期未完成时弹窗提醒

Machine
  不参与时间窗口判断，也不依赖 UI 或 MES 保存结果
```

这样 Machine 仍然只表达机台能力，App Orchestration 负责跨 UI、MES、配置的业务编排，避免把定时提醒逻辑散落在页面或设备层。