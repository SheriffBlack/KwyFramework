using System.IO.Ports;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Melsec;
using HslCommunication.Profinet.Omron;
using HslCommunication.Profinet.Panasonic;
using HslCommunication.Profinet.Siemens;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Hsl.Licensing;
using Kwy.Licensing.Abstractions;

namespace Kwy.Device.PLC.Hsl;

internal static class HslPlcClientFactory
{
    public static HslPlcClientSession Create(HslPlcConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        LicenseActivationResult activationResult = HslCommunicationLicenseActivator.ActivateDefault();
        if (!activationResult.Success)
        {
            throw new InvalidOperationException(activationResult.Message, activationResult.Exception);
        }

        HslPlcClientSession session = config.Connection switch
        {
            TcpPlcConnectionConfig tcp => CreateTcpClient(config, tcp),
            SerialPlcConnectionConfig serial => CreateSerialClient(config, serial),
            _ => throw new NotSupportedException($"不支持的 PLC 连接配置：{config.Connection.GetType().FullName}。")
        };

        if (session.Client is NetworkDoubleBase netBase)
        {
            netBase.ConnectTimeOut = config.ConnectTimeoutMilliseconds;
            netBase.ReceiveTimeOut = config.ReceiveTimeoutMilliseconds;
        }

        return session;
    }

    private static HslPlcClientSession CreateTcpClient(HslPlcConfig config, TcpPlcConnectionConfig connection)
    {
        return config.Brand switch
        {
            HslPlcBrandType.Siemens_S71200 => CreateSiemensClient(SiemensPLCS.S1200, config, connection),
            HslPlcBrandType.Siemens_S71500 => CreateSiemensClient(SiemensPLCS.S1500, config, connection),
            HslPlcBrandType.Siemens_S7300 => CreateSiemensClient(SiemensPLCS.S300, config, connection),
            HslPlcBrandType.Siemens_S7400 => CreateSiemensClient(SiemensPLCS.S400, config, connection),
            HslPlcBrandType.Siemens_S7200Smart => CreateSiemensClient(SiemensPLCS.S200Smart, config, connection),
            HslPlcBrandType.Mitsubishi_MC => CreateMelsecMcClient(connection, UsePort(connection.Port, 6000), "Mitsubishi MC TCP"),
            HslPlcBrandType.Mitsubishi_Fx3U => CreateMelsecA1EClient(connection, UsePort(connection.Port, 5000), "Mitsubishi FX3U TCP"),
            HslPlcBrandType.Mitsubishi_Fx5U => CreateMelsecMcClient(connection, UsePort(connection.Port, 5000), "Mitsubishi FX5U TCP"),
            HslPlcBrandType.Keyence_MC => CreateKeyenceMcClient(connection, UsePort(connection.Port, 8501)),
            HslPlcBrandType.Keyence_NanoSerialOverTcp => CreateKeyenceNanoSerialOverTcpClient(connection, UsePort(connection.Port, 8501)),
            HslPlcBrandType.Panasonic_MC => CreatePanasonicClient(connection, UsePort(connection.Port, 5002)),
            HslPlcBrandType.Omron_Fins => CreateOmronFinsClient(connection, UsePort(connection.Port, 9600)),
            HslPlcBrandType.Modbus_Tcp => CreateModbusTcpClient(connection, UsePort(connection.Port, 502)),
            _ => throw new NotSupportedException($"PLC brand '{config.Brand}' does not support TCP in this HSL wrapper.")
        };
    }

    private static HslPlcClientSession CreateSerialClient(HslPlcConfig config, SerialPlcConnectionConfig connection)
    {
        DeviceSerialPort serialClient = config.Brand switch
        {
            HslPlcBrandType.Modbus_Rtu => new ModbusRtu(config.Station),
            HslPlcBrandType.Mitsubishi_FxSerial => new MelsecFxSerial(),
            HslPlcBrandType.Panasonic_Mewtocol => new PanasonicMewtocol(config.Station),
            _ => throw new NotSupportedException($"PLC brand '{config.Brand}' does not support serial in this HSL wrapper.")
        };

        serialClient.SerialPortInni(
            connection.PortName,
            connection.BaudRate,
            connection.DataBits,
            ToStopBits(connection.StopBits),
            ToParity(connection.Parity));

        return new HslPlcClientSession(
            serialClient,
            serialClient.Open,
            () =>
            {
                serialClient.Close();
                return OperateResult.CreateSuccessResult();
            },
            $"{config.Brand} serial {connection.Endpoint}");
    }

    private static HslPlcClientSession CreateSiemensClient(SiemensPLCS plcType, HslPlcConfig config, TcpPlcConnectionConfig connection)
    {
        var client = new SiemensS7Net(plcType, connection.Host)
        {
            Port = UsePort(connection.Port, 102),
            Rack = config.Rack,
            Slot = config.Slot
        };

        return WrapTcp(client, $"{plcType} TCP");
    }

    private static HslPlcClientSession CreateMelsecMcClient(TcpPlcConnectionConfig connection, int port, string description)
    {
        var client = new MelsecMcNet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, description);
    }

    private static HslPlcClientSession CreateMelsecA1EClient(TcpPlcConnectionConfig connection, int port, string description)
    {
        var client = new MelsecA1ENet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, description);
    }

    private static HslPlcClientSession CreateKeyenceMcClient(TcpPlcConnectionConfig connection, int port)
    {
        var client = new HslCommunication.Profinet.Keyence.KeyenceMcNet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, "Keyence MC TCP");
    }

    private static HslPlcClientSession CreateKeyenceNanoSerialOverTcpClient(TcpPlcConnectionConfig connection, int port)
    {
        var client = new HslCommunication.Profinet.Keyence.KeyenceNanoSerialOverTcp(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, "Keyence NanoSerialOverTcp");
    }

    private static HslPlcClientSession CreatePanasonicClient(TcpPlcConnectionConfig connection, int port)
    {
        var client = new HslCommunication.Profinet.Panasonic.PanasonicMcNet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, "Panasonic MC TCP");
    }

    private static HslPlcClientSession CreateOmronFinsClient(TcpPlcConnectionConfig connection, int port)
    {
        var client = new OmronFinsNet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, "Omron FINS TCP");
    }

    private static HslPlcClientSession CreateModbusTcpClient(TcpPlcConnectionConfig connection, int port)
    {
        var client = new ModbusTcpNet(connection.Host, port);
        return WrapClient(client, client.ConnectServer, client.ConnectClose, "Modbus TCP");
    }

    private static HslPlcClientSession WrapTcp(SiemensS7Net client, string description)
        => WrapClient(client, client.ConnectServer, client.ConnectClose, description);

    private static HslPlcClientSession WrapClient(IReadWriteNet client, Func<OperateResult> connect, Func<OperateResult> disconnect, string description)
    {
        return new HslPlcClientSession(client, connect, disconnect, description);
    }

    private static int UsePort(int configuredPort, int defaultPort)
        => configuredPort > 0 ? configuredPort : defaultPort;

    private static Parity ToParity(ParityType parity)
        => parity switch
        {
            ParityType.Odd => Parity.Odd,
            ParityType.Even => Parity.Even,
            ParityType.Mark => Parity.Mark,
            ParityType.Space => Parity.Space,
            _ => Parity.None
        };

    private static StopBits ToStopBits(StopBitsType stopBits)
        => stopBits switch
        {
            StopBitsType.None => StopBits.None,
            StopBitsType.Two => StopBits.Two,
            StopBitsType.OnePointFive => StopBits.OnePointFive,
            _ => StopBits.One
        };
}
