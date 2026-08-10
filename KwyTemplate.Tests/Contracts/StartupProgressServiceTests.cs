using KwyTemplate.Contracts.Services;
using Xunit;

namespace KwyTemplate.Tests.Contracts;

public sealed class StartupProgressServiceTests
{
    [Fact]
    public void Report_ClampsProgressAndRaisesChangedEvent()
    {
        var service = new StartupProgressService();
        var events = new List<StartupProgressChangedEventArgs>();
        service.ProgressChanged += (_, e) => events.Add(e);

        service.Report("loading", 150);

        Assert.Equal("loading", service.CurrentItem);
        Assert.Equal(100, service.ProgressValue);
        Assert.Equal("[100%]", service.PercentText);
        Assert.False(service.IsCompleted);
        var item = Assert.Single(events);
        Assert.Equal(100, item.ProgressValue);
        Assert.False(item.IsCompleted);
    }

    [Fact]
    public void Complete_SetsProgressTo100AndCompleted()
    {
        var service = new StartupProgressService();

        service.Report("loading", 20);
        service.Complete("done");

        Assert.Equal("done", service.CurrentItem);
        Assert.Equal(100, service.ProgressValue);
        Assert.True(service.IsCompleted);
        Assert.Equal("[100%]", service.PercentText);
    }
}
