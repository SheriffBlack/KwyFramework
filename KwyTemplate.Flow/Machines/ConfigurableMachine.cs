using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions.IO;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Profiles;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// The standard machine runtime. Structure, devices and station IO are supplied by MachineProfile;
/// it reuses MachineBase polling, result dispatch and the existing instrument data deals.
/// </summary>
public sealed class ConfigurableMachine : MachineBase
{
    private readonly MachineProfile profile;
    private readonly IReadOnlyDictionary<string, MachineIoPointProfile> ioPoints;

    public ConfigurableMachine(IMachineDeviceContext devices, IMachineProfileProvider profileProvider)
        : base(devices)
    {
        profile = profileProvider?.GetActiveProfile() ?? throw new ArgumentNullException(nameof(profileProvider));
        ioPoints = profile.IoPoints.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        MachinePollingIntervalMs = profile.MachinePollingIntervalMs;
        IoSnapshotPollingIntervalMs = profile.IoSnapshotPollingIntervalMs;
        BindDevices();
        InitTestStations();
        BuildDataGrid();
    }

    public override string MachineId => profile.MachineId;

    public override string MachineName => profile.MachineName;

    public override TriggerMode StationTriggerMode => TriggerMode.Polling;

    public override void BindDevices()
    {
        foreach (MachinePlcPointProfile point in profile.PlcPoints)
        {
            if (string.IsNullOrWhiteSpace(point.Key) || string.IsNullOrWhiteSpace(point.Address))
            {
                continue;
            }

            PlcPointDefinitions.Add(new MachinePlcPointDefinition(
                point.Key,
                point.Address,
                string.IsNullOrWhiteSpace(point.DisplayName) ? point.Key : point.DisplayName,
                ResolveDataType(point.DataType),
                point.IsReadOnly));
        }

        MachineDeviceProfile? plcProfile = profile.Devices.FirstOrDefault(item => item.Kind == ConfigurableDeviceKind.MainPlc);
        if (plcProfile != null && Devices.TryGet<IPlcDevice>(plcProfile.DeviceId, out IPlcDevice? plc))
        {
            BindPlc(plc);
        }

        MachineDeviceProfile? ioProfile = profile.Devices.FirstOrDefault(item => item.Kind == ConfigurableDeviceKind.MainIoCard);
        if (ioProfile != null && Devices.TryGet<IIoCardDevice>(ioProfile.DeviceId, out IIoCardDevice? ioCard) && ioCard != null)
        {
            BindIoCard(ioCard);
            foreach (MachineIoPointProfile point in profile.IoPoints)
            {
                string displayName = string.IsNullOrWhiteSpace(point.DisplayName) ? point.Key : point.DisplayName;
                if (point.Direction == MachineIoPointDirection.Input)
                {
                    ioCard.SetDiName(point.Channel, displayName);
                }
                else
                {
                    ioCard.SetDoName(point.Channel, displayName);
                }
            }
        }
    }

    public override void InitTestStations()
    {
        TestStations = profile.Stations.Select(CreateStation).ToList();
    }

    private TestStationModel CreateStation(MachineStationProfile source)
    {
        var station = new TestStationModel
        {
            StationId = source.StationId,
            StationName = source.StationName,
            IconKind = Enum.TryParse(source.IconKind, true, out StationIconKind iconKind)
                ? iconKind
                : StationIconKind.Station,
            IsEnabled = source.IsEnabled,
            ShowInResultGrid = source.ShowInResultGrid,
            UseInstrumentConfigTestNames = source.UseInstrumentConfigTestNames,
            InstrumentDeviceIds = source.InstrumentDeviceIds.ToList(),
            OrderedTestNames = source.TestNames.ToList(),
            StationIo = new StationIoBinding
            {
                TestFinishedInput = ResolveIoChannel(source.Io.TestFinishedInputPoint, MachineIoPointDirection.Input, source.Io.TestFinishedInput),
                ResultOkInput = ResolveIoChannel(source.Io.ResultOkInputPoint, MachineIoPointDirection.Input, source.Io.ResultOkInput),
                ResultNgInput = ResolveIoChannel(source.Io.ResultNgInputPoint, MachineIoPointDirection.Input, source.Io.ResultNgInput),
                ResultReadCompletedOutput = ResolveIoChannel(source.Io.ResultReadCompletedOutputPoint, MachineIoPointDirection.Output, source.Io.ResultReadCompletedOutput),
                ResultOkOutput = ResolveIoChannel(source.Io.ResultOkOutputPoint, MachineIoPointDirection.Output, source.Io.ResultOkOutput),
                ResultNgOutput = ResolveIoChannel(source.Io.ResultNgOutputPoint, MachineIoPointDirection.Output, source.Io.ResultNgOutput),
                ResultSource = Enum.TryParse(source.Io.ResultSource, true, out StationResultSource resultSource)
                    ? resultSource
                    : StationResultSource.Hardware
            }
        };

        foreach (string testName in station.OrderedTestNames)
        {
            station.TestValues[testName] = 0;
            station.TestJudges[testName] = true;
        }

        foreach (string operation in source.Operations.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            station.Operations.Add(new StationOperationDescriptor { Code = operation, DisplayName = operation });
        }

        AddInstrumentDeals(station);
        if (station.StationDataDeals.Count == 0)
        {
            station.StationDataDeals.Add(new StationIoResultDataDeal(station.StationName));
        }

        return station;
    }

    private void AddInstrumentDeals(TestStationModel station)
    {
        IMeasurementInstrument[] instruments = station.InstrumentDeviceIds
            .Select(deviceId => Devices.TryGet<IMeasurementInstrument>(deviceId, out IMeasurementInstrument? instrument) ? instrument : null)
            .Where(static instrument => instrument != null)
            .Cast<IMeasurementInstrument>()
            .ToArray();

        if (instruments.Length == 1 && station.OrderedTestNames.Count > 1)
        {
            var mappings = station.OrderedTestNames
                .Select((testName, index) => new MeasurementValueMapping(testName, index))
                .ToArray();
            station.StationDataDeals.Add(new InstrumentMultiMeasurementDataDeal(instruments[0], mappings));
            return;
        }

        for (int index = 0; index < instruments.Length; index++)
        {
            string testName = station.OrderedTestNames.ElementAtOrDefault(index) ?? $"Value{index + 1}";
            station.StationDataDeals.Add(new InstrumentMeasurementDataDeal(testName, instruments[index]));
        }
    }

    private static Type ResolveDataType(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "INT16" or "SHORT" => typeof(short),
            "INT32" or "INT" => typeof(int),
            "UINT16" or "USHORT" => typeof(ushort),
            "UINT32" or "UINT" => typeof(uint),
            "DOUBLE" => typeof(double),
            "SINGLE" or "FLOAT" => typeof(float),
            _ => typeof(bool)
        };

    private int ResolveIoChannel(string? key, MachineIoPointDirection expectedDirection, int fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        if (!ioPoints.TryGetValue(key, out MachineIoPointProfile? point) || point.Direction != expectedDirection)
        {
            throw new InvalidOperationException($"IO point '{key}' is not a valid {expectedDirection} point.");
        }

        return point.Channel;
    }
}
