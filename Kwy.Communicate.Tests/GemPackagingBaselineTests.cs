using Kwy.Communicate.Gem;
using Kwy.Communicate.Gem300;

namespace Kwy.Communicate.Tests;

public sealed class GemPackagingBaselineTests
{
    [Fact]
    public void AreYouThereRequest_UsesStandardS1F1Identity()
    {
        using var message = GemMessageFactory.AreYouThereRequest();

        Assert.Equal(1, message.S);
        Assert.Equal(1, message.F);
        Assert.True(message.ReplyExpected);
    }

    [Fact]
    public void EmptySlotMap_CreatesOneBasedSlots()
    {
        SlotMap map = SlotMap.Empty(3);

        Assert.Equal([1, 2, 3], map.Slots.Select(static item => item.Slot));
        Assert.All(map.Slots, static item => Assert.False(item.HasSubstrate));
    }

    [Fact]
    public async Task InMemoryCarrierManager_StoresAndUpdatesCarrier()
    {
        var manager = new InMemoryCarrierManager();
        var carrier = new Carrier("C01", 1, SlotMap.Empty(2));

        await manager.RegisterCarrierAsync(carrier);
        await manager.UpdateCarrierStateAsync("C01", CarrierAccessState.InAccess);

        Assert.Equal(CarrierAccessState.InAccess, Assert.Single(manager.Carriers).AccessState);
    }
}
