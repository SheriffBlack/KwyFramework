using KwyTemplate.Contracts.Services;

namespace KwyTemplate.MES.Cyntec;

public sealed class CyntecMesOptions : IProductionOutputOptions
{
    public string IpAddress { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 13000;

    public int ReadTimeout { get; set; } = -1;

    public int WriteTimeout { get; set; } = -1;

    public string SetupDirectory { get; set; } = @"D:\MES\Setup";

    public string ProgramDirectory { get; set; } = @"D:\MES\Parameter";

    public string StandardPartDirectory { get; set; } = @"D:\MES\Stdpart";

    public string EquipmentDirectory { get; set; } = @"D:\MES\Equipment";

    public string LogDirectory { get; set; } = @"D:\MES\Log";

    public string OutputDirectory { get; set; } = @"D:\MES\Output";

    public string SummaryDirectory { get; set; } = @"D:\MES\Summary";

    public string SetupFileExtension { get; set; } = ".txt";

    public string StandardPartFileExtension { get; set; } = ".txt";

    public string EquipmentFileExtension { get; set; } = ".txt";

    public int LogRetentionDays { get; set; } = 31;
}

