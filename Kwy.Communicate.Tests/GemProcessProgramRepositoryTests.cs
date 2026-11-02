using Kwy.Communicate.Gem;
using Secs4Net;

namespace Kwy.Communicate.Tests;

public sealed class GemProcessProgramRepositoryTests
{
    [Fact]
    public async Task UnsupportedRepository_RejectsMutations()
    {
        var repository = new UnsupportedGemProcessProgramRepository();

        GemProcessProgramSaveResult save = await repository.SaveAsync(
            new GemProcessProgram("RCP-01", Item.A("body")));
        GemProcessProgramDeleteResult delete = await repository.DeleteAsync("RCP-01");

        Assert.Equal(GemProcessProgramSaveStatus.Rejected, save.Status);
        Assert.Equal(GemProcessProgramDeleteStatus.Rejected, delete.Status);
    }

    [Fact]
    public async Task SaveAsync_RequiresExplicitOverwriteForExistingPpid()
    {
        using var repository = new InMemoryGemProcessProgramRepository();
        using var original = new GemProcessProgram("RCP-01", Item.A("v1"), "1");
        using var updated = new GemProcessProgram("RCP-01", Item.A("v2"), "2");

        GemProcessProgramSaveResult first = await repository.SaveAsync(original);
        GemProcessProgramSaveResult duplicate = await repository.SaveAsync(updated);
        GemProcessProgramSaveResult overwrite = await repository.SaveAsync(
            updated,
            new GemProcessProgramSaveOptions(Overwrite: true));

        Assert.True(first.Succeeded);
        Assert.Equal(GemProcessProgramSaveStatus.AlreadyExists, duplicate.Status);
        Assert.True(overwrite.Succeeded);
        using GemProcessProgram? stored = await repository.FindAsync("RCP-01");
        Assert.Equal("2", stored?.Version);
    }

    [Fact]
    public async Task Repository_ListsAndDeletesProcessPrograms()
    {
        using var repository = new InMemoryGemProcessProgramRepository();
        using var b = new GemProcessProgram("B", Item.A("b"));
        using var a = new GemProcessProgram("A", Item.A("a"));
        await repository.SaveAsync(b);
        await repository.SaveAsync(a);

        IReadOnlyList<string> ppids = await repository.ListPpidsAsync();
        GemProcessProgramDeleteResult deleted = await repository.DeleteAsync("A");
        GemProcessProgramDeleteResult missing = await repository.DeleteAsync("A");

        Assert.Equal(new[] { "A", "B" }, ppids);
        Assert.True(deleted.Succeeded);
        Assert.Equal(GemProcessProgramDeleteStatus.NotFound, missing.Status);
    }
}
