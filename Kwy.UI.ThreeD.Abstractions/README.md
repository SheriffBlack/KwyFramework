# Kwy UI 3D 分层

```text
Kwy.UI.ThreeD.Abstractions             net8.0
├─ Geometry / Materials               后端无关的数据
├─ Scene                              场景、节点和光源契约
├─ State                              节点运行状态
├─ Updates                            运行时变更
└─ Rendering                          后端与视口契约

Kwy.UI.WPF.ThreeD                     net8.0-windows
└─ Controls/KwyThreeDView             WPF 门面

Kwy.UI.WPF.ThreeD.HelixToolkit
└─ Rendering/HelixThreeDBackend       DirectX/HelixToolkit 适配器

Kwy.UI.WPF.ThreeD.OpenGL
└─ Rendering/OpenGLThreeDBackend      未来 OpenGL 适配器
```

应用项目只需引用一个实现项目，例如 `Kwy.UI.WPF.ThreeD.HelixToolkit`；抽象层和 WPF 门面会由项目引用传递带入。

业务层通常通过 `ThreeDSceneBinding` 直接绑定节点、灯光和节点状态；高级场景可以使用 `IThreeDSceneSource` 描述基础场景，并通过 `ThreeDUpdatesBinding` 推送底层更新。XAML 只放置 `KwyThreeDView`。门面负责托管 `IThreeDSession<FrameworkElement>`，并提供相机、背景、导航、选中对象和命中命令等 WPF 绑定入口。更换渲染引擎时，只替换 `IThreeDBackend<FrameworkElement>` 实例，场景模型和业务交互不变。

```xml
<UserControl xmlns:threeD="http://schemas.kwy.com/ui/threed">
    <UserControl.Resources>
        <threeD:HelixThreeDBackend x:Key="ThreeDBackend" />
    </UserControl.Resources>

    <threeD:KwyThreeDView
        Backend="{StaticResource ThreeDBackend}"
        threeD:ThreeDSceneBinding.Nodes="{Binding Nodes}"
        threeD:ThreeDSceneBinding.Lights="{Binding Lights}"
        threeD:ThreeDSceneBinding.NodeState="{Binding LatestNodeState}"
        threeD:ThreeDSceneBinding.NodeStates="{Binding NodeStates}"
        threeD:ThreeDSceneBinding.ResetKey="{Binding SceneId}"
        Camera="{Binding Camera, Mode=TwoWay}"
        SelectedObject="{Binding SelectedObject, Mode=TwoWay}"
        HitTestCommand="{Binding SelectObjectCommand}"
        IsNavigationEnabled="True"
        Background="Transparent" />
</UserControl>
```

`ThreeDSceneBinding` 面向常规业务，自动把 `SceneNodeDescriptor`、`LightDescriptor` 和 `ThreeDNodeState` 转换为场景更新，并监听可观察集合的变化。`ThreeDUpdatesBinding` 面向需要直接构造 `ThreeDSceneUpdate` 的高级场景。只有设备或工艺数据无法直接映射为上述公共模型时，应用项目才需要编写专用行为。
