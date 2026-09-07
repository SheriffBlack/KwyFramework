# Kwy.Files.Excel 使用说明

`Kwy.Files.Excel` 采用“抽象层 + Provider 实现层”的结构：

```text
Kwy.Files.Excel.Abstractions
  只定义接口、模型、配置项，不依赖任何 Excel 第三方库。

Kwy.Files.Excel.Interop
  基于 Microsoft.Office.Interop.Excel，调用真实 Microsoft Excel。

Kwy.Files.Excel.NPOI
  基于 NPOI，不需要安装 Microsoft Excel。

Kwy.Files.Excel.EPPlus
  基于 EPPlus，主要面向 .xlsx 和模板复制场景。
```

业务项目通常只引用 `Kwy.Files.Excel.Abstractions` 和一个具体 Provider 项目。除非确实需要同时支持多种引擎，否则不要在同一个业务模块里同时注册多个默认 `IExcelWorkbookService`。

## 怎么选择 Provider

| Provider | 支持格式 | 是否需要安装 Excel | 推荐场景 |
| --- | --- | --- | --- |
| `Kwy.Files.Excel.NPOI` | `.xls` / `.xlsx` | 否 | 默认优先选择。适合工控机、服务端工具、普通读写 Excel。 |
| `Kwy.Files.Excel.EPPlus` | `.xlsx` | 否 | 模板复制、样式保留、图片保留、较新的 `.xlsx` 文件处理。 |
| `Kwy.Files.Excel.Interop` | `.xls` / `.xlsx` / `.csv` | 是 | 必须依赖真实 Excel 行为的场景，例如企业加密 Excel、宏/插件环境、Excel 自身公式计算或特殊兼容性。 |

建议优先级：

```text
普通读写：NPOI
模板报表：EPPlus
必须打开真实 Excel：Interop
```

## 安装与注册

### NPOI

项目引用：

```xml
<ProjectReference Include="..\Kwy.Files.Excel.Abstractions\Kwy.Files.Excel.Abstractions.csproj" />
<ProjectReference Include="..\Kwy.Files.Excel.NPOI\Kwy.Files.Excel.NPOI.csproj" />
```

DI 注册：

```csharp
services.AddKwyExcelNpoi();
```

### EPPlus

项目引用：

```xml
<ProjectReference Include="..\Kwy.Files.Excel.Abstractions\Kwy.Files.Excel.Abstractions.csproj" />
<ProjectReference Include="..\Kwy.Files.Excel.EPPlus\Kwy.Files.Excel.EPPlus.csproj" />
```

DI 注册：

```csharp
services.AddKwyExcelEpplus(options =>
{
    options.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
});
```

注意：EPPlus 有自己的许可要求。实际商业项目请按 EPPlus 官方许可选择合适的 `LicenseContext` 和授权方式。

### Interop

项目引用：

```xml
<ProjectReference Include="..\Kwy.Files.Excel.Abstractions\Kwy.Files.Excel.Abstractions.csproj" />
<ProjectReference Include="..\Kwy.Files.Excel.Interop\Kwy.Files.Excel.Interop.csproj" />
```

DI 注册：

```csharp
services.AddKwyExcelInterop(options =>
{
    options.Visible = false;
    options.DisplayAlerts = false;
    options.ScreenUpdating = false;
});
```

`Interop` 要求目标机器安装并正确注册 Microsoft Excel。它只适合 Windows 桌面/工控机环境，不建议用于无人值守服务端。

## 接口说明

### IExcelWorkbookService

`IExcelWorkbookService` 是工作簿入口服务，用于打开、创建、读取整个工作簿，以及获取 Sheet 名称。

```csharp
public interface IExcelWorkbookService
{
    ExcelProviderInfo ProviderInfo { get; }

    Task<IExcelWorkbookSession> OpenAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);

    Task<IExcelWorkbookSession> CreateAsync(ExcelFileFormat format = ExcelFileFormat.Xlsx, CancellationToken cancellationToken = default);

    Task<ExcelWorkbookData> ReadWorkbookAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSheetNamesAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);
}
```

各成员用途：

| 成员 | 用途 |
| --- | --- |
| `ProviderInfo` | 获取当前 Provider 的名称、支持能力和支持格式。 |
| `OpenAsync` | 打开已有 Excel 文件，返回一个 `IExcelWorkbookSession`。需要读写单元格时使用它。 |
| `CreateAsync` | 创建新的工作簿，返回一个 `IExcelWorkbookSession`。 |
| `ReadWorkbookAsync` | 一次性读取整个工作簿，适合只读导入场景。 |
| `GetSheetNamesAsync` | 只读取 Sheet 名称，不读取所有数据。 |

推荐写法：

```csharp
public sealed class ReportService
{
    private readonly IExcelWorkbookService excel;

    public ReportService(IExcelWorkbookService excel)
    {
        this.excel = excel;
    }

    public async Task WriteResultAsync(string filePath)
    {
        await using var session = await excel.OpenAsync(new ExcelOpenOptions
        {
            FilePath = filePath,
            ReadOnly = false
        });

        await session.WriteCellAsync("Sheet1", new ExcelCellAddress(1, 1), "OK");
        await session.SaveAsync();
    }
}
```

### IExcelWorkbookSession

`IExcelWorkbookSession` 表示一个已经打开或创建的工作簿。它是实际读写单元格和保存文件的对象。

```csharp
public interface IExcelWorkbookSession : IAsyncDisposable
{
    string? FilePath { get; }

    ExcelFileFormat Format { get; }

    Task<IReadOnlyList<string>> GetSheetNamesAsync(CancellationToken cancellationToken = default);

    Task<ExcelSheetData> ReadSheetAsync(ExcelReadOptions options, CancellationToken cancellationToken = default);

    Task<object?> ReadCellAsync(string sheetName, ExcelCellAddress address, CancellationToken cancellationToken = default);

    Task WriteCellAsync(string sheetName, ExcelCellAddress address, object? value, CancellationToken cancellationToken = default);

    Task WriteRangeAsync(ExcelWriteOptions options, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task SaveAsAsync(ExcelSaveOptions options, CancellationToken cancellationToken = default);
}
```

各成员用途：

| 成员 | 用途 |
| --- | --- |
| `FilePath` | 当前工作簿路径。新建但未保存的工作簿可能为空。 |
| `Format` | 当前工作簿格式，例如 `Xls`、`Xlsx`、`Csv`。 |
| `GetSheetNamesAsync` | 获取当前工作簿所有 Sheet 名称。 |
| `ReadSheetAsync` | 读取一个 Sheet 的二维数据。 |
| `ReadCellAsync` | 读取单个单元格。 |
| `WriteCellAsync` | 写入单个单元格。 |
| `WriteRangeAsync` | 从指定行列开始写入二维数据。 |
| `SaveAsync` | 保存到原路径。 |
| `SaveAsAsync` | 另存为指定路径。 |
| `DisposeAsync` | 关闭并释放当前工作簿资源。业务代码必须调用，推荐 `await using`。 |

读取 Sheet：

```csharp
await using var session = await excel.OpenAsync(new ExcelOpenOptions
{
    FilePath = "input.xlsx",
    ReadOnly = true
});

ExcelSheetData sheet = await session.ReadSheetAsync(new ExcelReadOptions
{
    SheetName = "Sheet1",
    StartRow = 1,
    StartColumn = 1,
    RowCount = 100,
    ColumnCount = 10,
    UseFormattedText = true
});

foreach (var row in sheet.Rows)
{
    string firstColumn = row.Count > 0 ? row[0]?.ToString() ?? string.Empty : string.Empty;
}
```

写入一块数据：

```csharp
var rows = new List<IReadOnlyList<object?>>
{
    new object?[] { "工站", "结果", "时间" },
    new object?[] { "A01", "OK", DateTime.Now },
    new object?[] { "A02", "NG", DateTime.Now }
};

await using var session = await excel.CreateAsync(ExcelFileFormat.Xlsx);

await session.WriteRangeAsync(new ExcelWriteOptions
{
    SheetName = "测试结果",
    StartRow = 1,
    StartColumn = 1,
    CreateSheetIfMissing = true,
    AutoFitColumns = true
}, rows);

await session.SaveAsAsync(new ExcelSaveOptions
{
    FilePath = "result.xlsx",
    Format = ExcelFileFormat.Xlsx,
    Overwrite = true
});
```

### IExcelTemplateService

`IExcelTemplateService` 面向模板复制。当前主要由 `Kwy.Files.Excel.EPPlus` 实现。

```csharp
public interface IExcelTemplateService
{
    Task CopySheetFromTemplateAsync(ExcelTemplateCopyOptions options, CancellationToken cancellationToken = default);
}
```

用途：

| 成员 | 用途 |
| --- | --- |
| `CopySheetFromTemplateAsync` | 从模板文件复制一个 Sheet 到目标文件，可指定新 Sheet 名称。 |

示例：

```csharp
await templateService.CopySheetFromTemplateAsync(new ExcelTemplateCopyOptions
{
    TemplateFilePath = "Template.xlsx",
    TemplateSheetName = "ReportTemplate",
    TargetFilePath = "Output.xlsx",
    NewSheetName = "Station-A",
    ReplaceIfExists = true,
    PreserveStyles = true,
    PreservePictures = true
});
```

说明：

```text
EPPlus 更适合模板复制。
NPOI 当前不注册 IExcelTemplateService。
Interop 当前不注册 IExcelTemplateService。
```

### IExcelSheetMergeService

`IExcelSheetMergeService` 用于把一个或多个 Excel 文件中的多个 Sheet 汇总到指定汇总 Sheet。

```csharp
public interface IExcelSheetMergeService
{
    Task<IReadOnlyDictionary<string, ExcelSheetMergeResult>> MergeFilesAsync(
        IEnumerable<string> filePaths,
        ExcelSheetMergeOptions options,
        IProgress<ExcelSheetMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

用途：

| 成员 | 用途 |
| --- | --- |
| `MergeFilesAsync` | 对多个文件逐个执行 Sheet 汇总，返回每个文件的处理结果。 |

示例：

```csharp
var progress = new Progress<ExcelSheetMergeProgress>(p =>
{
    Console.WriteLine($"{p.FileIndex}/{p.FileCount}: {p.FilePath}");
});

var results = await mergeService.MergeFilesAsync(
    new[] { "A.xlsx", "B.xlsx" },
    new ExcelSheetMergeOptions
    {
        SummarySheetName = "全表汇总",
        HeaderRow = 1,
        StartRow = 10,
        FilterCheckColumns = new[] { 1, 2, 3 },
        AdditionalColumnsCount = 2,
        AdditionalColumnsHeaders = new[] { "备注", "来源" }
    },
    progress);

foreach (var (file, result) in results)
{
    Console.WriteLine($"{file}: Success={result.Success}, Rows={result.TotalRows}");
}
```

`ExcelSheetMergeOptions` 说明：

| 属性 | 说明 |
| --- | --- |
| `SummarySheetName` | 汇总 Sheet 名称，默认 `全表汇总`。 |
| `StartRow` | 从源 Sheet 的第几行开始读取数据，默认 `10`。 |
| `HeaderRow` | 从源 Sheet 的第几行读取表头，默认 `1`。 |
| `HeaderContent` | 手动指定表头内容。设置后优先使用它。 |
| `AdditionalColumnsCount` | 额外追加的列数量。 |
| `AdditionalColumnsHeaders` | 额外追加列的表头。 |
| `FilterCheckColumns` | 检查指定列，如果这些列都为空，则跳过该行。列号从 1 开始。 |
| `ExcludeRules` | 排除规则，key 是 1-based 列号，value 是需要排除的文本集合。 |
| `ShowApplicationWindow` | Interop 场景可用于显示 Excel 窗口。 |
| `WaitForUserInput` | 预留给需要人工干预的 Interop 流程。 |

### IExcelActionQueue

`IExcelActionQueue` 是底层串行执行队列。它主要服务于 `Interop`，用于把 Excel COM 操作固定在同一个 STA 线程执行。

普通业务代码一般不需要直接使用它。

```csharp
public interface IExcelActionQueue : IAsyncDisposable
{
    Task RunAsync(Action action, CancellationToken cancellationToken = default);

    Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
}
```

它解决的问题：

```text
Excel COM 对线程模型敏感。
多个并发请求直接操作 COM 容易出现 RPC_E_CALL_REJECTED、Excel 正忙、对象被占用等问题。
ActionQueue 把操作排队到一个 STA 线程，避免并发交叉修改 Excel 对象。
```

## Option 类型说明

### ExcelOpenOptions

用于打开已有文件。

| 属性 | 说明 |
| --- | --- |
| `FilePath` | 文件路径，必填。 |
| `Format` | 文件格式。默认 `Auto`，根据扩展名识别。 |
| `ReadOnly` | 是否只读打开，默认 `true`。写入时必须设为 `false`。 |
| `Password` | 文件密码。Provider 支持情况不同。 |
| `AllowTransparentEncryptedRead` | 允许 Provider 使用企业透明加密读取能力。主要面向 Interop。 |

### ExcelReadOptions

用于读取 Sheet。

| 属性 | 说明 |
| --- | --- |
| `SheetName` | Sheet 名称。为空时读取第一个 Sheet。 |
| `StartRow` | 起始行，1-based，默认 `1`。 |
| `StartColumn` | 起始列，1-based，默认 `1`。 |
| `RowCount` | 最大读取行数。为空表示读到末尾。 |
| `ColumnCount` | 最大读取列数。为空表示读到末尾。 |
| `UseFormattedText` | 是否读取格式化文本。导入显示值时用 `true`，保留原始值时用 `false`。 |

### ExcelWriteOptions

用于批量写入数据。

| 属性 | 说明 |
| --- | --- |
| `SheetName` | 写入 Sheet 名称。 |
| `StartRow` | 起始行，1-based。 |
| `StartColumn` | 起始列，1-based。 |
| `CreateSheetIfMissing` | Sheet 不存在时是否自动创建。 |
| `AutoFitColumns` | 写入后是否自动调整列宽。 |

### ExcelSaveOptions

用于另存为文件。

| 属性 | 说明 |
| --- | --- |
| `FilePath` | 保存路径。 |
| `Format` | 保存格式。默认 `Auto`，根据扩展名识别。 |
| `Overwrite` | 文件存在时是否覆盖。 |
| `Password` | 保存密码。Provider 支持情况不同。 |

### ExcelTemplateCopyOptions

用于模板 Sheet 复制。

| 属性 | 说明 |
| --- | --- |
| `TemplateFilePath` | 模板文件路径。 |
| `TemplateSheetName` | 模板 Sheet 名称。 |
| `TargetFilePath` | 目标文件路径。不存在则创建。 |
| `NewSheetName` | 复制后的新 Sheet 名称。为空时使用模板 Sheet 名称。 |
| `ReplaceIfExists` | 目标 Sheet 已存在时是否替换。 |
| `PreserveStyles` | 是否保留样式。具体效果由 Provider 决定。 |
| `PreservePictures` | 是否保留图片。具体效果由 Provider 决定。 |

## Model 类型说明

### ExcelCellAddress

表示单元格地址，行列都是 1-based。

```csharp
var a1 = new ExcelCellAddress(1, 1);
var b2 = new ExcelCellAddress(2, 2);
```

构造时会校验行列必须大于等于 1。

### ExcelRangeAddress

表示一个区域地址，例如从 A1 到 C10。

```csharp
var range = new ExcelRangeAddress(
    new ExcelCellAddress(1, 1),
    new ExcelCellAddress(10, 3));
```

构造时会校验结束行列不能小于开始行列。

### ExcelSheetData

表示一个 Sheet 的二维数据。

```csharp
public string SheetName { get; set; }
public IReadOnlyList<IReadOnlyList<object?>> Rows { get; set; }
```

`Rows` 的第一层是行，第二层是列。这里不携带样式，只表示数据。

### ExcelWorkbookData

表示整个工作簿的数据。

```csharp
public string? SourcePath { get; set; }
public ExcelFileFormat Format { get; set; }
public List<ExcelSheetData> Sheets { get; }
```

### ExcelProviderInfo

表示 Provider 能力。

```csharp
ExcelProviderInfo info = workbookService.ProviderInfo;

Console.WriteLine(info.Name);
Console.WriteLine(info.Features);
```

可用于在 UI 上提示当前 Excel 引擎支持什么格式、是否支持模板、是否支持 Interop 自动化。

## 资源释放规则

业务代码只需要记住一条：

```csharp
await using var session = await workbookService.OpenAsync(...);
```

不要手动释放从 DI 注入的 `IExcelWorkbookService`。

### Interop 生命周期

```text
ExcelInteropApplication       Singleton
ExcelInteropWorkbookService   Singleton
Excel.Application COM 实例    懒加载并复用
ExcelInteropWorkbookSession   每次 Open/Create 新建
Workbook COM 对象             每个 Session 一个
```

也就是说，第二次打开文件时，默认复用同一个 `Excel.Application`，但会创建新的 WorkbookSession 和 Workbook。

`session.DisposeAsync()` 会关闭当前 Workbook。DI 容器释放时，`ExcelInteropApplication.Dispose()` 会退出 Excel 并释放 COM Application。

### NPOI 生命周期

```text
NpoiExcelWorkbookService      Singleton
NpoiExcelWorkbookSession      每次 Open/Create 新建
IWorkbook                     每个 Session 一个
```

`session.DisposeAsync()` 会关闭当前 `IWorkbook`。

### EPPlus 生命周期

```text
EpplusExcelWorkbookService    Singleton
EpplusExcelWorkbookSession    每次 Open/Create 新建
ExcelPackage                  每个 Session 一个
```

`session.DisposeAsync()` 会释放当前 `ExcelPackage`。

## 多 Provider 同时使用

如果一个业务项目同时需要 NPOI 和 EPPlus，不建议两个都注册为默认 `IExcelWorkbookService`，因为后注册的可能覆盖前面的默认服务。

更清晰的方式是直接注册具体类型，或自己写一个选择器：

```csharp
public enum ExcelProviderKind
{
    Npoi,
    Epplus,
    Interop
}

public interface IExcelProviderSelector
{
    IExcelWorkbookService GetWorkbookService(ExcelProviderKind kind);
}
```

常见项目里不需要这么复杂，选一个默认 Provider 即可。

## NPOI NU1903 警告

`NPOI 2.8.0` 曾经会传递引入 `System.Security.Cryptography.Xml 8.0.2`。NuGet audit 会对该版本报告 `NU1903`，因为它受到安全公告影响。

当前项目已经在 `Kwy.Files.Excel.NPOI.csproj` 显式覆盖为安全版本：

```xml
<PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.3" />
```

这样可以保持 `NPOI 2.8.0` 不变，同时让 NuGet 解析到修复后的 `System.Security.Cryptography.Xml 8.0.3`。

不要优先使用：

```xml
<NoWarn>$(NoWarn);NU1903</NoWarn>
```

`NoWarn` 只是隐藏警告，不会改变依赖图。只有在安全评审确认风险可接受时，才考虑压制警告。

验证命令：

```powershell
dotnet list Kwy.Files.Excel.NPOI\Kwy.Files.Excel.NPOI.csproj package --include-transitive
dotnet build Kwy.Files.Excel.NPOI\Kwy.Files.Excel.NPOI.csproj -f net8.0
dotnet build Kwy.Files.Excel.NPOI\Kwy.Files.Excel.NPOI.csproj -f net10.0
```

正常结果应为：

```text
System.Security.Cryptography.Xml 8.0.3
0 warning
0 error
```

## 常见问题

### 为什么坐标从 1 开始

Excel 用户习惯是第 1 行、第 1 列，所以 `ExcelCellAddress` 和所有 `StartRow` / `StartColumn` 都是 1-based。这样业务代码和 Excel UI 对得上。

### 读取显示文本还是原始值

`ExcelReadOptions.UseFormattedText` 决定读取方式：

```text
true  读取显示文本，例如日期格式、百分比格式后的内容。
false 读取原始值，例如数字、DateTime、bool。
```

导入给用户看的报表时通常用 `true`。做数据计算或二次处理时通常用 `false`。

### 什么时候用 SaveAsync，什么时候用 SaveAsAsync

```text
OpenAsync 打开的已有文件：修改后可以 SaveAsync。
CreateAsync 创建的新文件：第一次保存必须 SaveAsAsync。
需要另存为新路径：使用 SaveAsAsync。
```

### 为什么 Interop 不建议服务端使用

`Interop` 控制的是真实 Excel 进程。它依赖桌面环境、COM 注册、Office 安装状态、用户权限和 Excel 自身弹窗行为。无人值守服务端更推荐 NPOI 或 EPPlus。
