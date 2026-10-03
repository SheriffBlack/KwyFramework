# Kwy Icon Geometry Generator

将字体图标资源字典中的单字符 `system:String` 图标转换为 WPF `Geometry` 资源。

```powershell
dotnet run --project Kwy.IconGeometryGenerator -- `
  --font "C:\path\to\SegoeFluent.ttf" `
  --source "Kwy.UI.WPF\Styles\IconStyle.xaml" `
  --output "Kwy.UI.WPF\Styles\IconStyle.Geometry.xaml"
```

若现有资源字典中还含有自定义图标，使用 `--base` 仅替换同名的字体图标：

```powershell
dotnet run --project Kwy.IconGeometryGenerator -- `
  --font "Kwy.UI.WPF\Font\SegoeFluent.ttf" `
  --source "Kwy.IconGeometryGenerator\Samples\IconStyle.Font.xaml" `
  --base "Kwy.UI.WPF\Styles\IconStyle.xaml" `
  --output "Kwy.UI.WPF\Styles\IconStyle.Merged.xaml"
```

输出文件保留原资源字典中的其他资源，只将字体中存在的单字符图标资源替换为 `Geometry`。WPF 会将路径文本解析为适合绘制的几何对象，可直接供 `Path.Data` 或图标属性使用。请先检查生成结果，再替换原始资源文件。可用 `Samples/IconStyle.Font.xaml` 验证生成流程。

工具只转换字形轮廓，不验证图标语义是否匹配。字体版本与 Unicode 码位表必须来自同一图标集。
