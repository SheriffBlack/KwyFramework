# Kwy.ComponentModel

`Kwy.ComponentModel` 是 Kwy 的通用元数据层，不依赖 WPF、WinForms 或具体 UI 平台。

它负责描述“对象属性或业务参数应该如何被 UI 编辑器展示”，上层 UI 组件只负责渲染。

## 分层

```text
Kwy.ComponentModel
  Attributes
    InputTypeAttribute
    ItemsSourceAttribute
    GroupWidthAttribute
    PlcPointAttribute

  Metadata
    PropertyMetadataItem
    PropertyMetadataReader

  Parameters
    KwyParameterDefinition
    KwyParameterMetadataReader
```

## Metadata

`PropertyMetadataReader` 用于读取普通 CLR 对象的属性元数据。

它支持：

- `CategoryAttribute`
- `DisplayNameAttribute`
- `DescriptionAttribute`
- `BrowsableAttribute`
- `ReadOnlyAttribute`
- `InputTypeAttribute`
- `ItemsSourceAttribute`
- `GroupWidthAttribute`
- 枚举候选项读取

典型用途：

```csharp
var properties = PropertyMetadataReader.GetProperties(typeof(MyConfig));
```

这适合 `KwyPropertyGrid` 这类组件从普通配置对象生成属性编辑界面。

## Parameters

`KwyParameterDefinition` 是通用参数定义模型。

它不要求背后一定存在 CLR 属性，适合流程节点、动态表单、算法参数、设备指令参数等场景。

核心字段：

- `Key`：参数键。
- `DisplayName`：显示名称。
- `Category`：分组。
- `ValueType`：值类型。
- `DefaultValue`：默认值。
- `Description`：说明。
- `InputType`：建议输入控件类型。
- `ItemsSource`：候选项。
- `GroupWidth`：分组宽度比例。
- `IsRequired`：是否必填。
- `IsReadOnly`：是否只读。
- `IsBrowsable`：是否显示。

示例：

```csharp
var threshold = KwyParameterDefinition.Create<double>(
    key: "Threshold",
    displayName: "阈值",
    defaultValue: 128.0,
    category: "图像处理",
    description: "二值化阈值");
```

## 从对象属性转换为参数定义

`KwyParameterMetadataReader` 可以把 CLR 属性元数据转换为 `KwyParameterDefinition`：

```csharp
var parameters = KwyParameterMetadataReader.GetParameters(config);
```

这样可以统一两种来源：

```text
普通配置对象 + 特性
    -> PropertyMetadataReader
    -> KwyParameterDefinition
    -> UI 参数编辑器

流程节点 Descriptor
    -> KwyParameterDefinition
    -> UI 参数编辑器
```

## 与 KwyTemplate.Vision 的关系

`KwyTemplate.Vision` 的节点 Descriptor 使用 `KwyParameterDefinition` 描述节点参数。

Descriptor 负责声明 UI 元数据，Executor 只负责运行逻辑：

```csharp
public IReadOnlyList<KwyParameterDefinition> Parameters =>
[
    KwyParameterDefinition.Create<double>("阈值", defaultValue: 128.0)
];
```

这样视觉流程平台、属性表格和普通配置对象可以共享同一套参数元数据模型。
