namespace KwyPecvd.Process.Equipment;

public interface IEquipmentModuleRegistry
{
    IReadOnlyCollection<IEquipmentModuleController> Modules { get; }

    void Add(IEquipmentModuleController module);

    bool TryGet<TModule>(string id, out TModule module)
        where TModule : class, IEquipmentModuleController;

    TModule GetRequired<TModule>(string id)
        where TModule : class, IEquipmentModuleController;

    IReadOnlyCollection<TModule> GetAll<TModule>()
        where TModule : class, IEquipmentModuleController;
}