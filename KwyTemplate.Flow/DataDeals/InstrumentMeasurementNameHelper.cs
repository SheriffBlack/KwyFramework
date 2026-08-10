using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Instruments.Lcr;

namespace KwyTemplate.Flow.DataDeals;

public static class InstrumentMeasurementNameHelper
{
    public static IReadOnlyList<MeasurementValueMapping> CreateMappings(IMeasurementInstrument? meter)
    {
        if (meter?.DeviceParameter is HiokiLcrConfig hiokiConfig)
        {
            return CreateHiokiMappings(hiokiConfig);
        }

        return [];
    }

    public static IReadOnlyList<string> CreateTestNames(IMeasurementInstrument? meter)
        => CreateMappings(meter).Select(static item => item.TestName).ToArray();

    private static IReadOnlyList<MeasurementValueMapping> CreateHiokiMappings(HiokiLcrConfig config)
    {
        HiokiLcrParameterPair activeParameters = config.GetActiveParameterPair();
        List<MeasurementValueMapping> mappings = [];
        AddHiokiMapping(mappings, activeParameters.Parameter1, 0);
        AddHiokiMapping(mappings, activeParameters.Parameter3, 1);
        return mappings;
    }

    private static void AddHiokiMapping(ICollection<MeasurementValueMapping> mappings, string parameter, int valueIndex)
    {
        string testName = HiokiLcrLoadTypes.ToDisplayParameter(parameter);
        if (!string.IsNullOrWhiteSpace(testName))
        {
            mappings.Add(new MeasurementValueMapping(testName, valueIndex));
        }
    }
}