namespace Kwy.ComponentModel;

[AttributeUsage(AttributeTargets.Field)]
public class PlcPointAttribute : Attribute
{
    public string Address { get; }
    public Type DataType { get; }
    public bool IsReadOnly { get; set; } // 是否只能读不能写

    public PlcPointAttribute(string address, Type dataType)
    {
        Address = address;
        DataType = dataType;
    }
}
