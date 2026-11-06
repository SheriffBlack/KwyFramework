# KwyTemplate.Equipment 设计说明

`KwyTemplate.Equipment` 是整机协调层，回答“整机当前是否允许执行某个生产命令”。

## 职责

- 维护整机生产状态。
- 组合 `IEquipmentCommandPolicy` 完成命令准入。
- 将获准的启动、停止命令交给 `KwyTemplate.Process` 执行。
- 为 App/Shell 提供统一的 `IEquipmentController` 入口。

## 边界

- 不创建、连接或解析硬件协议；这些属于 `KwyTemplate.Device`。
- 不实现工位步骤、测量或结果判定；这些属于 `KwyTemplate.Process`。
- 不引用 ViewModel、XAML 或弹窗服务。

## 依赖方向

```text
KwyTemplate.App / KwyTemplate.Shell
                 |
                 v
       KwyTemplate.Equipment
                 |
                 v
        KwyTemplate.Process
                 |
                 v
         KwyTemplate.Device
```

MES 校验、用户确认等交互仍由 App 完成；最终生产命令必须通过 `IEquipmentController`。
