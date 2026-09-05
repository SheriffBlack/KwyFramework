using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.MES.Cyntec;

/// <summary>Cyntec CSV 键值对工单文件适配器。</summary>
public sealed class CyntecWorkOrderSetupFileParser : IWorkOrderSetupFileParser
{
    public MesWorkOrderSetup Parse(string workOrderNo, string filePath)
        => CyntecMesFileParser.ParseWorkOrderSetup(workOrderNo, filePath);
}
