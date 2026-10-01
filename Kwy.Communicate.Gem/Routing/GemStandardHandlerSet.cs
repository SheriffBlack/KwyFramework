namespace Kwy.Communicate.Gem;

/// <summary>为设备侧会话一次注册当前已实现的标准 Primary Message Handler。</summary>
public static class GemStandardHandlerSet
{
    public static GemPrimaryMessageRouter RegisterEquipmentHandlers(
        GemPrimaryMessageRouter router,
        GemRegistry registry,
        IGemEquipmentSession session,
        Func<IReadOnlyList<GemEquipmentConstantChange>, CancellationToken, Task<bool>> equipmentConstantWriter,
        Func<GemAlarmEnableRequest, CancellationToken, Task<bool>> alarmEnablementWriter)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(equipmentConstantWriter);
        ArgumentNullException.ThrowIfNull(alarmEnablementWriter);

        return router
            .Register(new S1F3Handler(registry))
            .Register(new S2F15Handler(registry, session, equipmentConstantWriter))
            .Register(new S2F23Handler(registry, session))
            .Register(new S2F41Handler(session))
            .Register(new S5F3Handler(registry, session, alarmEnablementWriter))
            .Register(new S7F3Handler(session));
    }
}
