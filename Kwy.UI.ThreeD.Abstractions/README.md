# Kwy UI 3D 分层

```text
Kwy.UI.ThreeD.Abstractions             net8.0
├─ Geometry / Materials               后端无关的数据
├─ Scene                              场景、节点和光源契约
├─ Updates                            运行时变更
└─ Rendering                          后端与视口契约

Kwy.UI.WPF.ThreeD                     net8.0-windows
└─ Controls/KwyThreeDViewport         WPF 门面

Kwy.UI.WPF.ThreeD.HelixToolkit
└─ Rendering/HelixThreeDBackend       DirectX/HelixToolkit 适配器

Kwy.UI.WPF.ThreeD.OpenGL
└─ Rendering/OpenGLThreeDBackend      未来 OpenGL 适配器
```

应用项目只需引用一个实现项目，例如 `Kwy.UI.WPF.ThreeD.HelixToolkit`；抽象层和 WPF 门面会由项目引用传递带入。

业务层用 `IThreeDSceneSource` 描述场景，用 `ThreeDSceneUpdate` 更新节点；XAML 只放置 `KwyThreeDViewport`。更换渲染引擎时，只替换 `IThreeDBackend<FrameworkElement>` 实例，场景模型和业务交互不变。
