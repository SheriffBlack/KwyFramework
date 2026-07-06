# Kwy.Files

`Kwy.Files` 提供文本、JSON、XML、INI、路径与文件系统相关的通用操作。

## INI 文件

`IniHelper` 的命名和同步/异步 API 与现有 `TextFileHelper`、`JsonHelper`、`XmlHelper` 保持一致。

```csharp
IniDocument document = IniHelper.Read("gts.cfg", Encoding.ASCII);

int alarmIndex = document["axis1"].GetInt32("alarmIndex");
double stopDeceleration = document["profile1"].GetDouble("decSmoothStop");
bool active = document["axis1"].GetBoolean("active");

document["axis1"].SetValue("alarmIndex", 1);
IniHelper.Write("gts.cfg", document, Encoding.ASCII);
```

异步操作：

```csharp
IniDocument document = await IniHelper.ReadAsync("gts.cfg", Encoding.ASCII, cancellationToken);
await IniHelper.WriteAsync("gts.cfg", document, Encoding.ASCII, cancellationToken: cancellationToken);
```

支持：

- `=` 和 `:` 分隔符。
- `;` 和 `#` 注释。
- section、key 名称不区分大小写。
- 十进制、`0x` 十六进制、浮点数和布尔值转换。
- section/key 顺序、注释、空行、原始换行符和末尾换行保留。
- 重复 section/key 检测。
- 默认使用同目录临时文件进行原子覆盖。

`.cfg` 只是扩展名。只有内容采用 INI 结构时才应使用 `IniHelper`。固高控制卡运行时仍应通过 `GT_LoadConfig()` 加载配置，`IniHelper` 主要用于检查、比较、诊断和配置工具。
