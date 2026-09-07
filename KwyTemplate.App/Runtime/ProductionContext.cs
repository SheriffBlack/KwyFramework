using Kwy.MVVM.Core;

namespace KwyTemplate.App.Runtime;

public sealed class ProductionContext : BindableBase, IProductionContext
{
    private string workOrderNo = string.Empty;
    private string tablePaperCode = string.Empty;
    private string topCoverCode = string.Empty;
    private string operatorNo = string.Empty;
    private string equipmentNo = string.Empty;
    private string machineType = string.Empty;
    private string reelMatNo = string.Empty;
    private string barcodeContent = string.Empty;
    private string reelTpNo = string.Empty;
    private string reelWorkOrderNo = string.Empty;
    private string reelId = string.Empty;
    private ReelScanState reelScanState;
    private bool isResultGridDataEnabled;

    public string WorkOrderNo { get => workOrderNo; set => SetProperty(ref workOrderNo, value ?? string.Empty); }
    public string TablePaperCode { get => tablePaperCode; set => SetProperty(ref tablePaperCode, value ?? string.Empty); }
    public string TopCoverCode { get => topCoverCode; set => SetProperty(ref topCoverCode, value ?? string.Empty); }
    public string OperatorNo { get => operatorNo; set => SetProperty(ref operatorNo, value ?? string.Empty); }
    public string EquipmentNo { get => equipmentNo; set => SetProperty(ref equipmentNo, value ?? string.Empty); }
    public string MachineType { get => machineType; set => SetProperty(ref machineType, value ?? string.Empty); }
    public string ReelMatNo { get => reelMatNo; set => SetProperty(ref reelMatNo, value ?? string.Empty); }
    public string BarcodeContent { get => barcodeContent; set => SetProperty(ref barcodeContent, value ?? string.Empty); }
    public string ReelTpNo { get => reelTpNo; set => SetProperty(ref reelTpNo, value ?? string.Empty); }
    public string ReelWorkOrderNo { get => reelWorkOrderNo; set => SetProperty(ref reelWorkOrderNo, value ?? string.Empty); }
    public string ReelId { get => reelId; set => SetProperty(ref reelId, value ?? string.Empty); }
    public ReelScanState ReelScanState { get => reelScanState; set => SetProperty(ref reelScanState, value); }
    public bool IsResultGridDataEnabled { get => isResultGridDataEnabled; set => SetProperty(ref isResultGridDataEnabled, value); }
}