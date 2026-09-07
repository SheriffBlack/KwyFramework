using OfficeOpenXml;

namespace Kwy.Files.Excel.EPPlus;

public sealed class ExcelEpplusOptions
{
    public LicenseContext LicenseContext { get; set; } = LicenseContext.NonCommercial;
}
