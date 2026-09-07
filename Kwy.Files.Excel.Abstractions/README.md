# Kwy.Files.Excel.Abstractions

`Kwy.Files.Excel.Abstractions` 是 Kwy Excel 文件能力的抽象层，只包含接口、模型、选项和通用工具。

它不引用：

```text
EPPlus
NPOI
Microsoft.Office.Interop.Excel
WPF
MVVM
```

这样业务层可以只面向统一接口开发，再根据部署环境选择具体 Provider。

## Provider 项目

```text
Kwy.Files.Excel.NPOI
  基于 NPOI，支持 .xls/.xlsx，不需要安装 Microsoft Excel。

Kwy.Files.Excel.EPPlus
  基于 EPPlus，面向 .xlsx 和模板复制场景。

Kwy.Files.Excel.Interop
  基于 Microsoft.Office.Interop.Excel，调用真实 Excel。
```

详细使用方式、接口职责、Provider 选择、DI 注册、资源释放和 NPOI 警告处理，请看：

[EXCEL_PROVIDERS.md](EXCEL_PROVIDERS.md)

## 核心接口

```csharp
IExcelWorkbookService
IExcelWorkbookSession
IExcelTemplateService
IExcelSheetMergeService
IExcelActionQueue
```

## 常用模型

```csharp
ExcelCellAddress
ExcelRangeAddress
ExcelSheetData
ExcelWorkbookData
ExcelProviderInfo
```

## 最小示例

```csharp
await using var session = await excel.OpenAsync(new ExcelOpenOptions
{
    FilePath = "report.xlsx",
    ReadOnly = false
});

await session.WriteCellAsync("Sheet1", new ExcelCellAddress(1, 1), "OK");
await session.SaveAsync();
```
