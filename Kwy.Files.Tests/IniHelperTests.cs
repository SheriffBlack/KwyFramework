using System.Text;
using Kwy.Files;
using Xunit;

namespace Kwy.Files.Tests;

public sealed class IniHelperTests
{
    private const string GtsContent =
        "; GTS configuration\r\n" +
        "\r\n" +
        "[profile1]\r\n" +
        "active=1\r\n" +
        "decSmoothStop=1.000000\r\n" +
        "\r\n" +
        "[axis1]\r\n" +
        "active=1\r\n" +
        "alarmIndex=-1\r\n" +
        "prfMap=0x1\r\n";

    [Fact]
    public void Parse_ReadsGtsValuesAndPreservesOriginalText()
    {
        IniDocument document = IniHelper.Parse(GtsContent);

        Assert.True(document["profile1"].GetBoolean("active"));
        Assert.Equal(1.0, document["profile1"].GetDouble("decSmoothStop"));
        Assert.Equal(-1, document["axis1"].GetInt32("alarmIndex"));
        Assert.Equal(1, document["axis1"].GetInt32("prfMap"));
        Assert.Equal(GtsContent, IniHelper.Serialize(document));
    }

    [Fact]
    public void SetValue_ChangesOnlyTargetLineAndKeepsInlineComment()
    {
        const string content = "[axis1]\r\nalarmIndex = -1  ; disabled\r\nactive=1\r\n";
        IniDocument document = IniHelper.Parse(content);

        document["axis1"].SetValue("alarmIndex", 1);

        Assert.Equal("[axis1]\r\nalarmIndex = 1  ; disabled\r\nactive=1\r\n", IniHelper.Serialize(document));
    }

    [Fact]
    public void GetOrAddSection_AddsValuesInOrder()
    {
        IniDocument document = IniHelper.Parse("[axis1]\nactive=1\n");
        IniSection section = document.GetOrAddSection("home1");
        section.SetValue("active", true);
        section.SetValue("filterTime", 10);

        Assert.Equal(
            "[axis1]\nactive=1\n\n[home1]\nactive=true\nfilterTime=10\n",
            IniHelper.Serialize(document));
    }

    [Fact]
    public void Parse_RejectsDuplicateKeys()
    {
        Assert.Throws<FormatException>(() => IniHelper.Parse("[axis1]\nactive=1\nactive=0\n"));
    }

    [Fact]
    public async Task WriteAsync_AtomicallyWritesAndReadsDocument()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Kwy.Files.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "motion.cfg");
        try
        {
            IniDocument document = IniHelper.Parse(GtsContent);
            document["axis1"].SetValue("alarmIndex", 1);

            await IniHelper.WriteAsync(filePath, document, Encoding.ASCII);
            IniDocument restored = await IniHelper.ReadAsync(filePath, Encoding.ASCII);

            Assert.Equal(1, restored["axis1"].GetInt32("alarmIndex"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ExistingGoogolCfgFiles_RoundTripWithoutChanges()
    {
        const string cfgDirectory = @"D:\Code\CFG";
        if (!Directory.Exists(cfgDirectory))
        {
            return;
        }

        string[] files = Directory.GetFiles(cfgDirectory, "*.cfg");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string original = File.ReadAllText(file, Encoding.ASCII);
            IniDocument document = IniHelper.Parse(original);

            Assert.NotEmpty(document.Sections);
            Assert.Equal(original, IniHelper.Serialize(document));
        }
    }
}
