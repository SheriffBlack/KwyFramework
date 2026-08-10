using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.Device.Connections.Editors;

/// <summary>
/// Reusable TCP/IP connection UI metadata wrapper.
/// </summary>
public sealed class TcpConnectionEditorModel
{
    private string host = "192.168.0.10";
    private int port;
    private int connectTimeoutMilliseconds = 3000;
    private int receiveTimeoutMilliseconds = 3000;
    private int sendTimeoutMilliseconds = 3000;
    private readonly Func<string>? hostGetter;
    private readonly Action<string>? hostSetter;
    private readonly Func<int>? portGetter;
    private readonly Action<int>? portSetter;
    private readonly Func<int>? connectTimeoutGetter;
    private readonly Action<int>? connectTimeoutSetter;
    private readonly Func<int>? receiveTimeoutGetter;
    private readonly Action<int>? receiveTimeoutSetter;
    private readonly Func<int>? sendTimeoutGetter;
    private readonly Action<int>? sendTimeoutSetter;

    public TcpConnectionEditorModel()
    {
    }

    public TcpConnectionEditorModel(
        Func<string> hostGetter,
        Action<string> hostSetter,
        Func<int> portGetter,
        Action<int> portSetter,
        Func<int> connectTimeoutGetter,
        Action<int> connectTimeoutSetter,
        Func<int> receiveTimeoutGetter,
        Action<int> receiveTimeoutSetter,
        Func<int>? sendTimeoutGetter = null,
        Action<int>? sendTimeoutSetter = null)
    {
        this.hostGetter = hostGetter ?? throw new ArgumentNullException(nameof(hostGetter));
        this.hostSetter = hostSetter ?? throw new ArgumentNullException(nameof(hostSetter));
        this.portGetter = portGetter ?? throw new ArgumentNullException(nameof(portGetter));
        this.portSetter = portSetter ?? throw new ArgumentNullException(nameof(portSetter));
        this.connectTimeoutGetter = connectTimeoutGetter ?? throw new ArgumentNullException(nameof(connectTimeoutGetter));
        this.connectTimeoutSetter = connectTimeoutSetter ?? throw new ArgumentNullException(nameof(connectTimeoutSetter));
        this.receiveTimeoutGetter = receiveTimeoutGetter ?? throw new ArgumentNullException(nameof(receiveTimeoutGetter));
        this.receiveTimeoutSetter = receiveTimeoutSetter ?? throw new ArgumentNullException(nameof(receiveTimeoutSetter));
        this.sendTimeoutGetter = sendTimeoutGetter;
        this.sendTimeoutSetter = sendTimeoutSetter;
    }

    [Category("TCP/IP")]
    [CategoryKey("Connection.Category.TcpIp")]
    [DisplayName("主机地址")]
    [DisplayNameKey("Connection.Host")]
    public string Host
    {
        get => hostGetter?.Invoke() ?? host;
        set
        {
            if (hostSetter != null)
            {
                hostSetter(value);
            }
            else
            {
                host = value;
            }
        }
    }

    [Category("TCP/IP")]
    [CategoryKey("Connection.Category.TcpIp")]
    [DisplayName("端口")]
    [DisplayNameKey("Connection.Port")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 65535, SmallChange = 1, DecimalPlaces = 0)]
    public int Port
    {
        get => portGetter?.Invoke() ?? port;
        set
        {
            if (portSetter != null)
            {
                portSetter(value);
            }
            else
            {
                port = value;
            }
        }
    }

    [Category("超时")]
    [CategoryKey("Connection.Category.Timeout")]
    [DisplayName("连接超时(ms)")]
    [DisplayNameKey("Connection.ConnectTimeout")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int ConnectTimeoutMilliseconds
    {
        get => connectTimeoutGetter?.Invoke() ?? connectTimeoutMilliseconds;
        set
        {
            if (connectTimeoutSetter != null)
            {
                connectTimeoutSetter(value);
            }
            else
            {
                connectTimeoutMilliseconds = value;
            }
        }
    }

    [Category("超时")]
    [CategoryKey("Connection.Category.Timeout")]
    [DisplayName("接收超时(ms)")]
    [DisplayNameKey("Connection.ReceiveTimeout")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int ReceiveTimeoutMilliseconds
    {
        get => receiveTimeoutGetter?.Invoke() ?? receiveTimeoutMilliseconds;
        set
        {
            if (receiveTimeoutSetter != null)
            {
                receiveTimeoutSetter(value);
            }
            else
            {
                receiveTimeoutMilliseconds = value;
            }
        }
    }

    [Category("超时")]
    [CategoryKey("Connection.Category.Timeout")]
    [DisplayName("发送超时(ms)")]
    [DisplayNameKey("Connection.SendTimeout")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int SendTimeoutMilliseconds
    {
        get => sendTimeoutGetter?.Invoke() ?? sendTimeoutMilliseconds;
        set
        {
            if (sendTimeoutSetter != null)
            {
                sendTimeoutSetter(value);
            }
            else
            {
                sendTimeoutMilliseconds = value;
            }
        }
    }
}

