using KwyTemplate.Flow.Services;
using Xunit;

namespace KwyTemplate.Tests.Flow;

public sealed class ProductionRecordWriterTests
{
    [Fact]
    public void BuildFileName_UsesFallbackWhenWorkOrderMissing()
    {
        string fileName = ProductionRecordPathHelper.BuildFileName(" ", "Machine_2_A");

        Assert.Equal("Machine_2_A.txt", fileName);
    }

    [Fact]
    public void BuildFileName_ReplacesInvalidFileNameCharacters()
    {
        string fileName = ProductionRecordPathHelper.BuildFileName("WO:001/02", "fallback");

        Assert.Equal("WO_001_02.txt", fileName);
    }

    [Fact]
    public async Task TryEnqueueAndFlush_WritesSequentialLines()
    {
        string directory = CreateTempDirectory();
        using var writer = new ProductionRecordWriter();

        Assert.True(writer.TryEnqueue(new ProductionRecordWriteRequest(directory, "WO001.txt", ["DCR1", "OK"])));
        Assert.True(writer.TryEnqueue(new ProductionRecordWriteRequest(directory, "WO001.txt", ["DCR2", "NG"])));

        await writer.FlushAsync();

        string[] lines = await File.ReadAllLinesAsync(Path.Combine(directory, "WO001.txt"));
        Assert.Equal(["1,DCR1,OK", "2,DCR2,NG"], lines);
    }

    [Fact]
    public async Task MoveAsync_ArchivesExistingTargetAndMovesRuntimeFile()
    {
        string sourceDirectory = CreateTempDirectory();
        string targetDirectory = CreateTempDirectory();
        using var writer = new ProductionRecordWriter();

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "WO001.txt"), "new-data");
        await File.WriteAllTextAsync(Path.Combine(targetDirectory, "WO001.txt"), "old-data");

        bool moved = await writer.MoveAsync(sourceDirectory, "WO001.txt", targetDirectory);

        Assert.True(moved);
        Assert.False(File.Exists(Path.Combine(sourceDirectory, "WO001.txt")));
        Assert.Equal("new-data", await File.ReadAllTextAsync(Path.Combine(targetDirectory, "WO001.txt")));
        string archiveDirectory = Path.Combine(targetDirectory, "Archive");
        string archivedFile = Assert.Single(Directory.GetFiles(archiveDirectory, "WO001_*.txt"));
        Assert.Equal("old-data", await File.ReadAllTextAsync(archivedFile));
    }

    [Fact]
    public async Task MoveAsync_WhenSourceMissing_ReturnsFalse()
    {
        using var writer = new ProductionRecordWriter();

        bool moved = await writer.MoveAsync(CreateTempDirectory(), "missing.txt", CreateTempDirectory());

        Assert.False(moved);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "KwyTemplateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
