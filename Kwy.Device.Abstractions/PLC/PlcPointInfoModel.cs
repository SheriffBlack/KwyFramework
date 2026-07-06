namespace Kwy.Device.Abstractions.PLC;

public class PlcPointInfoModel
{
    public required string Address { get; set; }
    public required string Name { get; set; }
    public required Type DataType { get; set; }
    public bool IsReadOnly { get; set; }
}
