using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Communicate.NI;

namespace KwyTemplate.Device.Connections.Editors;

/// <summary>
/// Reusable GPIB connection UI metadata wrapper.
/// </summary>
public sealed class GpibConnectionEditorModel
{
    private int boardNumber;
    private int primaryAddress = 1;
    private int secondaryAddress;
    private int timeout = 10000;
    private bool keepAlive = true;
    private int keepAliveInterval = 1000;
    private string? keepAliveCommand;
    private bool autoReconnect = true;
    private int maxReconnectAttempts = 3;
    private int reconnectInterval = 1000;

    private readonly Func<int>? boardNumberGetter;
    private readonly Action<int>? boardNumberSetter;
    private readonly Func<int>? primaryAddressGetter;
    private readonly Action<int>? primaryAddressSetter;
    private readonly Func<int>? secondaryAddressGetter;
    private readonly Action<int>? secondaryAddressSetter;
    private readonly Func<int>? timeoutGetter;
    private readonly Action<int>? timeoutSetter;
    private readonly Func<bool>? keepAliveGetter;
    private readonly Action<bool>? keepAliveSetter;
    private readonly Func<int>? keepAliveIntervalGetter;
    private readonly Action<int>? keepAliveIntervalSetter;
    private readonly Func<string?>? keepAliveCommandGetter;
    private readonly Action<string?>? keepAliveCommandSetter;
    private readonly Func<bool>? autoReconnectGetter;
    private readonly Action<bool>? autoReconnectSetter;
    private readonly Func<int>? maxReconnectAttemptsGetter;
    private readonly Action<int>? maxReconnectAttemptsSetter;
    private readonly Func<int>? reconnectIntervalGetter;
    private readonly Action<int>? reconnectIntervalSetter;

    public GpibConnectionEditorModel()
    {
    }

    public GpibConnectionEditorModel(GpibConfig source)
        : this(
            () => source.BoardNumber,
            value => source.BoardNumber = value,
            () => source.PrimaryAddress,
            value => source.PrimaryAddress = value,
            () => source.SecondaryAddress,
            value => source.SecondaryAddress = value,
            () => source.Timeout,
            value => source.Timeout = value,
            () => source.KeepAlive,
            value => source.KeepAlive = value,
            () => source.KeepAliveInterval,
            value => source.KeepAliveInterval = value,
            () => source.KeepAliveCommand,
            value => source.KeepAliveCommand = value,
            () => source.AutoReconnect,
            value => source.AutoReconnect = value,
            () => source.MaxReconnectAttempts,
            value => source.MaxReconnectAttempts = value,
            () => source.ReconnectInterval,
            value => source.ReconnectInterval = value)
    {
    }

    public GpibConnectionEditorModel(
        Func<int> boardNumberGetter,
        Action<int> boardNumberSetter,
        Func<int> primaryAddressGetter,
        Action<int> primaryAddressSetter,
        Func<int> secondaryAddressGetter,
        Action<int> secondaryAddressSetter,
        Func<int> timeoutGetter,
        Action<int> timeoutSetter,
        Func<bool> keepAliveGetter,
        Action<bool> keepAliveSetter,
        Func<int> keepAliveIntervalGetter,
        Action<int> keepAliveIntervalSetter,
        Func<string?>? keepAliveCommandGetter = null,
        Action<string?>? keepAliveCommandSetter = null,
        Func<bool>? autoReconnectGetter = null,
        Action<bool>? autoReconnectSetter = null,
        Func<int>? maxReconnectAttemptsGetter = null,
        Action<int>? maxReconnectAttemptsSetter = null,
        Func<int>? reconnectIntervalGetter = null,
        Action<int>? reconnectIntervalSetter = null)
    {
        this.boardNumberGetter = boardNumberGetter ?? throw new ArgumentNullException(nameof(boardNumberGetter));
        this.boardNumberSetter = boardNumberSetter ?? throw new ArgumentNullException(nameof(boardNumberSetter));
        this.primaryAddressGetter = primaryAddressGetter ?? throw new ArgumentNullException(nameof(primaryAddressGetter));
        this.primaryAddressSetter = primaryAddressSetter ?? throw new ArgumentNullException(nameof(primaryAddressSetter));
        this.secondaryAddressGetter = secondaryAddressGetter ?? throw new ArgumentNullException(nameof(secondaryAddressGetter));
        this.secondaryAddressSetter = secondaryAddressSetter ?? throw new ArgumentNullException(nameof(secondaryAddressSetter));
        this.timeoutGetter = timeoutGetter ?? throw new ArgumentNullException(nameof(timeoutGetter));
        this.timeoutSetter = timeoutSetter ?? throw new ArgumentNullException(nameof(timeoutSetter));
        this.keepAliveGetter = keepAliveGetter ?? throw new ArgumentNullException(nameof(keepAliveGetter));
        this.keepAliveSetter = keepAliveSetter ?? throw new ArgumentNullException(nameof(keepAliveSetter));
        this.keepAliveIntervalGetter = keepAliveIntervalGetter ?? throw new ArgumentNullException(nameof(keepAliveIntervalGetter));
        this.keepAliveIntervalSetter = keepAliveIntervalSetter ?? throw new ArgumentNullException(nameof(keepAliveIntervalSetter));
        this.keepAliveCommandGetter = keepAliveCommandGetter;
        this.keepAliveCommandSetter = keepAliveCommandSetter;
        this.autoReconnectGetter = autoReconnectGetter;
        this.autoReconnectSetter = autoReconnectSetter;
        this.maxReconnectAttemptsGetter = maxReconnectAttemptsGetter;
        this.maxReconnectAttemptsSetter = maxReconnectAttemptsSetter;
        this.reconnectIntervalGetter = reconnectIntervalGetter;
        this.reconnectIntervalSetter = reconnectIntervalSetter;
    }

    [Category("GPIB")]
    [CategoryKey("Connection.Category.Gpib")]
    [DisplayName("板卡号")]
    [DisplayNameKey("Connection.BoardNumber")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 32, SmallChange = 1, DecimalPlaces = 0)]
    public int BoardNumber
    {
        get => boardNumberGetter?.Invoke() ?? boardNumber;
        set
        {
            if (boardNumberSetter != null)
            {
                boardNumberSetter(value);
            }
            else
            {
                boardNumber = value;
            }
        }
    }

    [Category("GPIB")]
    [CategoryKey("Connection.Category.Gpib")]
    [DisplayName("主地址")]
    [DisplayNameKey("Connection.PrimaryAddress")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 30, SmallChange = 1, DecimalPlaces = 0)]
    public int PrimaryAddress
    {
        get => primaryAddressGetter?.Invoke() ?? primaryAddress;
        set
        {
            if (primaryAddressSetter != null)
            {
                primaryAddressSetter(value);
            }
            else
            {
                primaryAddress = value;
            }
        }
    }

    [Category("GPIB")]
    [CategoryKey("Connection.Category.Gpib")]
    [DisplayName("次地址")]
    [DisplayNameKey("Connection.SecondaryAddress")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 30, SmallChange = 1, DecimalPlaces = 0)]
    public int SecondaryAddress
    {
        get => secondaryAddressGetter?.Invoke() ?? secondaryAddress;
        set
        {
            if (secondaryAddressSetter != null)
            {
                secondaryAddressSetter(value);
            }
            else
            {
                secondaryAddress = value;
            }
        }
    }

    [Category("超时")]
    [CategoryKey("Connection.Category.Timeout")]
    [DisplayName("超时时间(ms)")]
    [DisplayNameKey("Connection.Timeout")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int Timeout
    {
        get => timeoutGetter?.Invoke() ?? timeout;
        set
        {
            if (timeoutSetter != null)
            {
                timeoutSetter(value);
            }
            else
            {
                timeout = value;
            }
        }
    }

    [Category("心跳")]
    [CategoryKey("Connection.Category.KeepAlive")]
    [DisplayName("启用心跳")]
    [DisplayNameKey("Connection.KeepAlive")]
    public bool KeepAlive
    {
        get => keepAliveGetter?.Invoke() ?? keepAlive;
        set
        {
            if (keepAliveSetter != null)
            {
                keepAliveSetter(value);
            }
            else
            {
                keepAlive = value;
            }
        }
    }

    [Category("心跳")]
    [CategoryKey("Connection.Category.KeepAlive")]
    [DisplayName("心跳间隔(ms)")]
    [DisplayNameKey("Connection.KeepAliveInterval")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int KeepAliveInterval
    {
        get => keepAliveIntervalGetter?.Invoke() ?? keepAliveInterval;
        set
        {
            if (keepAliveIntervalSetter != null)
            {
                keepAliveIntervalSetter(value);
            }
            else
            {
                keepAliveInterval = value;
            }
        }
    }

    [Category("心跳")]
    [CategoryKey("Connection.Category.KeepAlive")]
    [DisplayName("心跳命令")]
    [DisplayNameKey("Connection.KeepAliveCommand")]
    public string? KeepAliveCommand
    {
        get => keepAliveCommandGetter?.Invoke() ?? keepAliveCommand;
        set
        {
            if (keepAliveCommandSetter != null)
            {
                keepAliveCommandSetter(value);
            }
            else
            {
                keepAliveCommand = value;
            }
        }
    }

    [Category("重连")]
    [CategoryKey("Connection.Category.Reconnect")]
    [DisplayName("启用自动重连")]
    [DisplayNameKey("Connection.AutoReconnect")]
    public bool AutoReconnect
    {
        get => autoReconnectGetter?.Invoke() ?? autoReconnect;
        set
        {
            if (autoReconnectSetter != null)
            {
                autoReconnectSetter(value);
            }
            else
            {
                autoReconnect = value;
            }
        }
    }

    [Category("重连")]
    [CategoryKey("Connection.Category.Reconnect")]
    [DisplayName("最大重连次数")]
    [DisplayNameKey("Connection.MaxReconnectAttempts")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 1000, SmallChange = 1, DecimalPlaces = 0)]
    public int MaxReconnectAttempts
    {
        get => maxReconnectAttemptsGetter?.Invoke() ?? maxReconnectAttempts;
        set
        {
            if (maxReconnectAttemptsSetter != null)
            {
                maxReconnectAttemptsSetter(value);
            }
            else
            {
                maxReconnectAttempts = value;
            }
        }
    }

    [Category("重连")]
    [CategoryKey("Connection.Category.Reconnect")]
    [DisplayName("重连间隔(ms)")]
    [DisplayNameKey("Connection.ReconnectInterval")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int ReconnectInterval
    {
        get => reconnectIntervalGetter?.Invoke() ?? reconnectInterval;
        set
        {
            if (reconnectIntervalSetter != null)
            {
                reconnectIntervalSetter(value);
            }
            else
            {
                reconnectInterval = value;
            }
        }
    }
}
