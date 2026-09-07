using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

/// <summary>
/// 将客户提供的工单参数文件转换为统一的工单设定。
/// 具体格式由各 MES 客户适配层实现，应用层不依赖客户文件格式。
/// </summary>
public interface IWorkOrderSetupFileParser
{
    MesWorkOrderSetup Parse(string workOrderNo, string filePath);
}
