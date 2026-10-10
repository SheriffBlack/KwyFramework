using KwyPecvd.Process.Equipment;

namespace KwyPecvd.Tests.Process;

public sealed class EquipmentModuleRegistryTests
{
    [Fact]
    public void Add_AllowsGettingSameModuleById()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var pm1 = new FakeChamberController(
            "PM1");

        // Act
        registry.Add(pm1);

        var found = registry
            .GetRequired<IFakeChamberController>(
                "PM1");

        // Assert
        Assert.Same(pm1, found);
    }

    [Fact]
    public void GetRequired_IgnoresIdCase()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var pm1 = new FakeChamberController(
            "PM1");

        registry.Add(pm1);

        // Act
        var found = registry
            .GetRequired<IFakeChamberController>(
                "pm1");

        // Assert
        Assert.Same(pm1, found);
    }

    [Fact]
    public void Add_AllowsSameInstanceMoreThanOnce()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var pm1 = new FakeChamberController(
            "PM1");

        // Act
        registry.Add(pm1);
        registry.Add(pm1);

        // Assert
        var registered = Assert.Single(
            registry.Modules);

        Assert.Same(pm1, registered);
    }

    [Fact]
    public void Add_RejectsDifferentInstanceWithSameId()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var first = new FakeChamberController(
            "PM1");

        var second = new FakeChamberController(
            "PM1");

        registry.Add(first);

        // Act + Assert
        var exception = Assert.Throws<
            InvalidOperationException>(
            () => registry.Add(second));

        Assert.Contains(
            "PM1",
            exception.Message);
    }

    [Fact]
    public void GetRequired_RejectsWrongControllerCapability()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        registry.Add(
            new FakeChamberController("PM1"));

        // Act + Assert：PM1是Chamber，不是LoadLock
        var exception = Assert.Throws<
            InvalidOperationException>(
            () => registry
                .GetRequired<IFakeLoadLockController>(
                    "PM1"));

        Assert.Contains(
            typeof(IFakeLoadLockController).FullName!,
            exception.Message);
    }

    [Fact]
    public void GetAll_ReturnsOnlyRequestedControllerCapability()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var pm1 = new FakeChamberController(
            "PM1");

        var pm2 = new FakeChamberController(
            "PM2");

        var ll1 = new FakeLoadLockController(
            "LL1");

        registry.Add(pm1);
        registry.Add(pm2);
        registry.Add(ll1);

        // Act
        var chambers = registry
            .GetAll<IFakeChamberController>();

        // Assert
        Assert.Equal(2, chambers.Count);
        Assert.Contains(pm1, chambers);
        Assert.Contains(pm2, chambers);
        Assert.DoesNotContain(
            chambers,
            chamber => chamber.Id == "LL1");
    }

    private interface IFakeChamberController
        : IEquipmentModuleController
    {
    }

    private interface IFakeLoadLockController
        : IEquipmentModuleController
    {
    }

    private sealed class FakeChamberController
        : IFakeChamberController
    {
        public FakeChamberController(
            string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    private sealed class FakeLoadLockController
        : IFakeLoadLockController
    {
        public FakeLoadLockController(
            string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}