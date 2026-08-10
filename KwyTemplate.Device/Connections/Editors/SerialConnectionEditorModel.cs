using System.ComponentModel;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.ComponentModel;

namespace KwyTemplate.Device.Connections.Editors;

/// <summary>
/// Reusable serial port connection UI metadata wrapper.
/// </summary>
public sealed class SerialConnectionEditorModel
{
    private string portName = "COM1";
    private int baudRate = 9600;
    private int dataBits = 8;
    private ParityType parity = ParityType.None;
    private StopBitsType stopBits = StopBitsType.One;
    private readonly Func<string>? portNameGetter;
    private readonly Action<string>? portNameSetter;
    private readonly Func<int>? baudRateGetter;
    private readonly Action<int>? baudRateSetter;
    private readonly Func<int>? dataBitsGetter;
    private readonly Action<int>? dataBitsSetter;
    private readonly Func<ParityType>? parityGetter;
    private readonly Action<ParityType>? paritySetter;
    private readonly Func<StopBitsType>? stopBitsGetter;
    private readonly Action<StopBitsType>? stopBitsSetter;

    public SerialConnectionEditorModel()
    {
    }

    public SerialConnectionEditorModel(
        Func<string> portNameGetter,
        Action<string> portNameSetter,
        Func<int> baudRateGetter,
        Action<int> baudRateSetter,
        Func<int> dataBitsGetter,
        Action<int> dataBitsSetter,
        Func<ParityType> parityGetter,
        Action<ParityType> paritySetter,
        Func<StopBitsType> stopBitsGetter,
        Action<StopBitsType> stopBitsSetter)
    {
        this.portNameGetter = portNameGetter ?? throw new ArgumentNullException(nameof(portNameGetter));
        this.portNameSetter = portNameSetter ?? throw new ArgumentNullException(nameof(portNameSetter));
        this.baudRateGetter = baudRateGetter ?? throw new ArgumentNullException(nameof(baudRateGetter));
        this.baudRateSetter = baudRateSetter ?? throw new ArgumentNullException(nameof(baudRateSetter));
        this.dataBitsGetter = dataBitsGetter ?? throw new ArgumentNullException(nameof(dataBitsGetter));
        this.dataBitsSetter = dataBitsSetter ?? throw new ArgumentNullException(nameof(dataBitsSetter));
        this.parityGetter = parityGetter ?? throw new ArgumentNullException(nameof(parityGetter));
        this.paritySetter = paritySetter ?? throw new ArgumentNullException(nameof(paritySetter));
        this.stopBitsGetter = stopBitsGetter ?? throw new ArgumentNullException(nameof(stopBitsGetter));
        this.stopBitsSetter = stopBitsSetter ?? throw new ArgumentNullException(nameof(stopBitsSetter));
    }

    [Category("串口")]
    [CategoryKey("Connection.Category.Serial")]
    [DisplayName("串口号")]
    [DisplayNameKey("Connection.PortName")]
    public string PortName
    {
        get => portNameGetter?.Invoke() ?? portName;
        set
        {
            if (portNameSetter != null)
            {
                portNameSetter(value);
            }
            else
            {
                portName = value;
            }
        }
    }

    [Category("串口")]
    [CategoryKey("Connection.Category.Serial")]
    [DisplayName("波特率")]
    [DisplayNameKey("Connection.BaudRate")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 10000000, SmallChange = 100, DecimalPlaces = 0)]
    public int BaudRate
    {
        get => baudRateGetter?.Invoke() ?? baudRate;
        set
        {
            if (baudRateSetter != null)
            {
                baudRateSetter(value);
            }
            else
            {
                baudRate = value;
            }
        }
    }

    [Category("串口")]
    [CategoryKey("Connection.Category.Serial")]
    [DisplayName("数据位")]
    [DisplayNameKey("Connection.DataBits")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("5", "6", "7", "8")]
    public int DataBits
    {
        get => dataBitsGetter?.Invoke() ?? dataBits;
        set
        {
            if (dataBitsSetter != null)
            {
                dataBitsSetter(value);
            }
            else
            {
                dataBits = value;
            }
        }
    }

    [Category("串口")]
    [CategoryKey("Connection.Category.Serial")]
    [DisplayName("校验位")]
    [DisplayNameKey("Connection.Parity")]
    public ParityType Parity
    {
        get => parityGetter?.Invoke() ?? parity;
        set
        {
            if (paritySetter != null)
            {
                paritySetter(value);
            }
            else
            {
                parity = value;
            }
        }
    }

    [Category("串口")]
    [CategoryKey("Connection.Category.Serial")]
    [DisplayName("停止位")]
    [DisplayNameKey("Connection.StopBits")]
    public StopBitsType StopBits
    {
        get => stopBitsGetter?.Invoke() ?? stopBits;
        set
        {
            if (stopBitsSetter != null)
            {
                stopBitsSetter(value);
            }
            else
            {
                stopBits = value;
            }
        }
    }
}

