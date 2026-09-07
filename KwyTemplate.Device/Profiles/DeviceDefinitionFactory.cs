using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.Instruments.Lcr;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Instruments;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// 按设备选择配置创建设备定义。
/// Catalog 只描述大机型，工位内的仪表型号差异交给这里收敛。
/// </summary>
internal static class DeviceDefinitionFactory
{
    public static DeviceDefinition CreateDcrMeter(
        int index,
        DcrMeterModel model,
        IProtocolConfig connectionConfig,
        IDeviceConfig parameterConfig)
    {
        string deviceId = DeviceIds.Instrument("Dcr", index);
        string deviceName = $"DCR{index}";

        return model switch
        {
            DcrMeterModel.AdexDcr => new AdexDcrDeviceDefinition(
                deviceId,
                deviceName,
                connectionConfig,
                parameterConfig as AdexDcrConfig ?? new AdexDcrConfig()),

            DcrMeterModel.HiokiLcr => new HiokiLcrDeviceDefinition(
                deviceId,
                deviceName,
                connectionConfig,
                parameterConfig as HiokiLcrConfig ?? new HiokiLcrConfig()),

            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
        };
    }
}
