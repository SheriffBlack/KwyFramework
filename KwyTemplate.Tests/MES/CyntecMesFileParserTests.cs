using KwyTemplate.MES.Cyntec;
using KwyTemplate.MES.Abstract.Models;
using Xunit;

namespace KwyTemplate.Tests.MES;

public sealed class CyntecMesFileParserTests
{
    [Fact]
    public void ParseWorkOrderSetup_MapsDcrMaterialAndTapeSetup()
    {
        string filePath = CreateTempFile([
            "1,DCRMaxValue,25.5",
            "2,DCRMinValue,19",
            "3,DCRUnit,mΩ",
            "4,DCRRange,1Ω",
            "5,DCRMaxValue2,26.5",
            "6,DCRMinValue2,18.5",
            "7,DCRUnit2,mΩ",
            "8,TablePaperMatNo,9120011966",
            "9,TopCoverMatNo,9120011377",
            "10,PackageQty,3003",
            "11,BeforeSpaceQty,105",
            "12,AfterSpaceQty,106",
            "13,BlankQty,5",
            "14,SampleQty,2",
            "15,MatGroupNo,WHTC",
            "16,StdPartsCheck,4"
        ]);

        var setup = CyntecMesFileParser.ParseWorkOrderSetup("WO001", filePath);

        Assert.Equal("WO001", setup.WorkOrderNo);
        Assert.Equal("WHTC", setup.RecipeName);
        Assert.Equal(4, setup.StandardSampleCheckInterval);
        Assert.NotNull(setup.MaterialRequirements);
        Assert.Equal("9120011966", setup.MaterialRequirements!.TablePaperMatNo);
        Assert.Equal("9120011377", setup.MaterialRequirements.TopCoverMatNo);
        Assert.NotNull(setup.TapeSetup);
        Assert.Equal(105, setup.TapeSetup!.BeforeSpaceQty);
        Assert.Equal(3003, setup.TapeSetup.PackageQty);
        Assert.Equal(106, setup.TapeSetup.AfterSpaceQty);
        Assert.Equal(2, setup.TapeSetup.SampleQty);
        Assert.Equal(5, setup.TapeSetup.BlankQty);
        Assert.Equal(5, setup.TapeSetup.BackNoFilmQty);

        var dcr1 = Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "DCR1");
        Assert.Equal(19, dcr1.LowerLimit);
        Assert.Equal(25.5, dcr1.UpperLimit);
        Assert.Equal("mΩ", dcr1.Unit);
        Assert.Equal("1Ω", dcr1.Range);

        var dcr2 = Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "DCR2");
        Assert.Equal(18.5, dcr2.LowerLimit);
        Assert.Equal(26.5, dcr2.UpperLimit);
        Assert.Equal("mΩ", dcr2.Unit);
        Assert.Equal("1Ω", dcr2.Range);
    }

    [Fact]
    public void ParseStandardSampleSetup_ParsesMultipleLimitsAndNormalizesLcrToLs()
    {
        string filePath = CreateTempFile([
            "1,2026/03/19 00:00:00,15,2026/04/02 00:00:00,True,DCR,DCR,22.8000,24.8000,22.8000,mΩ,,,WHTC,01,Std0009,DCR standard",
            "2,2026/03/19 00:00:00,15,2026/04/02 00:00:00,True,Ls,LCR,1.2000,1.5000,1.0000,H,1000,HZ,WHTC,01,Std0010,Ls standard",
            "3,2026/03/19 00:00:00,15,2026/04/02 00:00:00,True,Rs,RS,0.1000,0.2000,0.0500,Ω,1000,HZ,WHTC,01,Std0011,Rs standard"
        ]);

        var setup = CyntecMesFileParser.ParseStandardSampleSetup("WO001", "STD001", filePath);

        Assert.Equal("WO001", setup.WorkOrderNo);
        Assert.Equal("STD001", setup.SampleCode);
        Assert.Equal(3, setup.MeasurementLimits.Count);

        var dcr = Assert.Single(setup.MeasurementLimits, item => item.ParameterId == "DCR");
        Assert.Equal(22.8, dcr.LowerLimit);
        Assert.Equal(24.8, dcr.UpperLimit);
        Assert.Equal(22.8, dcr.StandardValue);
        Assert.Equal("mΩ", dcr.Unit);

        var ls = Assert.Single(setup.MeasurementLimits, item => item.ParameterId == "LS");
        Assert.Equal(1.0, ls.LowerLimit);
        Assert.Equal(1.5, ls.UpperLimit);
        Assert.Equal(1.2, ls.StandardValue);
        Assert.Equal("H", ls.Unit);
        Assert.Equal("1000", ls.Frequency);
        Assert.Equal("HZ", ls.FrequencyUnit);

        var rs = Assert.Single(setup.MeasurementLimits, item => item.ParameterId == "RS");
        Assert.Equal(0.05, rs.LowerLimit);
        Assert.Equal(0.2, rs.UpperLimit);
        Assert.Equal(0.1, rs.StandardValue);
        Assert.Equal("Ω", rs.Unit);
    }

    [Fact]
    public void ParseWorkOrderSetup_LEnable2Disabled_DoesNotSuppressPrimaryLsRs()
    {
        string filePath = CreateTempFile([
            "1,RSMaxValue,830",
            "2,RSMinValue,400",
            "3,RSUnit,mΩ",
            "4,LEnable2,N",
            "5,LMaxValue,0.5546",
            "6,LMinValue,0.3854",
            "7,LUnit,μH",
            "8,LCRRange,300Ω"
        ]);

        var setup = CyntecMesFileParser.ParseWorkOrderSetup("WO-LSRS", filePath);

        var ls = Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Ls");
        Assert.Equal(0.3854, ls.LowerLimit);
        Assert.Equal(0.5546, ls.UpperLimit);
        Assert.Equal("μH", ls.Unit);

        var rs = Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Rs");
        Assert.Equal(400, rs.LowerLimit);
        Assert.Equal(830, rs.UpperLimit);
        Assert.Equal("mΩ", rs.Unit);
    }

    [Fact]
    public void ParseWorkOrderSetup_PrimaryLsRsRemainEnabledAndUseFirstFrequency()
    {
        string filePath = CreateTempFile([
            "1,RSMaxValue,830",
            "2,RSMinValue,400",
            "3,RSUnit,mΩ",
            "4,RSFreq,",
            "5,LEnable2,N",
            "6,LMaxValue,0.5546",
            "7,LMinValue,0.3854",
            "8,LUnit,μH",
            "9,LFreq,5000",
            "10,LCRRange,300Ω"
        ]);

        var setup = CyntecMesFileParser.ParseWorkOrderSetup("WO-LSRS-FREQ", filePath);

        Assert.Equal(0.3854, Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Ls").LowerLimit);
        Assert.Equal(830, Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Rs").UpperLimit);
        Assert.True(setup.Parameters.TryGetDouble("LFreq", out double frequency));
        Assert.Equal(5000, frequency);
    }

    [Fact]
    public void ParseWorkOrderSetup_UsesMilliOhmForCyntecZWhenNoUnitIsProvided()
    {
        string filePath = CreateTempFile([
            "1,ZEnable,Y",
            "2,ZMaxValue1,6",
            "3,ZMinValue1,0.5",
            "4,ZMaxValue2,7",
            "5,ZMinValue2,0.6"
        ]);

        var setup = CyntecMesFileParser.ParseWorkOrderSetup("WO-Z", filePath);

        Assert.Equal("mΩ", Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Z1").Unit);
        Assert.Equal("mΩ", Assert.Single(setup.InstrumentSetups!, item => item.ParameterId == "Z2").Unit);
    }

    [Fact]
    public void ParseStandardSampleSetup_WhenFileMissing_ReturnsEmptySetupWithDataSource()
    {
        string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt");

        var setup = CyntecMesFileParser.ParseStandardSampleSetup("WO001", "STD001", filePath);

        Assert.Empty(setup.MeasurementLimits);
        Assert.Equal(filePath, setup.DataSource?.Location);
    }

    [Fact]
    public void StandardSampleCheckEquipmentFile_UsesLegacyUploadStrColumnLayout()
    {
        string directory = Path.Combine(Path.GetTempPath(), "KwyTemplateTests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "EQ001.txt");
        var request = new MesStandardSampleCheckSaveRequest(
            new MesRequestContext("EQ001", "Machine", "OP001", "WO001"),
            "WO001",
            "Std0009",
            true,
            DateTimeOffset.Now,
            [
                new("DCR", "DCR", 22.7, true, Unit: "mΩ", SampleId: "Std0009", MeterType: "DCR", MeterSerialNo: "01", ItemName: "DCR"),
                new("LS", "Ls", 0.427, true, Unit: "μH", SampleId: "Std0009", MeterType: "LCR", MeterSerialNo: "01", ItemName: "LCR-5M", Frequency: "5", FrequencyUnit: "MHz"),
                new("RS", "Rs", 0.596, true, Unit: "Ω", SampleId: "Std0009", MeterType: "RS", MeterSerialNo: "01", ItemName: "RS-5M", Frequency: "5", FrequencyUnit: "MHz")
            ]);

        CyntecStandardSampleCheckFileWriter.Write(filePath, request);

        Assert.Equal(
        [
            "1,Std0009,DCR,1,DCR,0,0,22.7,OK",
            "2,Std0009,LCR-5M,1,LCR,5,MHZ,0.427,OK",
            "3,Std0009,RS-5M,1,RS,5,MHZ,0.596,OK"
        ], File.ReadAllLines(filePath));
    }

    private static string CreateTempFile(IEnumerable<string> lines)
    {
        string directory = Path.Combine(Path.GetTempPath(), "KwyTemplateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "data.txt");
        File.WriteAllLines(filePath, lines);
        return filePath;
    }
}

