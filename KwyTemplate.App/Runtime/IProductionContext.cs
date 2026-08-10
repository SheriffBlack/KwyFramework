using KwyTemplate.Contracts.Services;

namespace KwyTemplate.App.Runtime;

public interface IProductionContext : IProductionRuntimeContext
{
    string TablePaperCode { get; set; }
    string TopCoverCode { get; set; }
    string EquipmentNo { get; set; }
    string MachineType { get; set; }
    string ReelMatNo { get; set; }
    string BarcodeContent { get; set; }
    string ReelTpNo { get; set; }
    string ReelWorkOrderNo { get; set; }
    string ReelId { get; set; }
    ReelScanState ReelScanState { get; set; }
}