namespace Kwy.Communicate.Gem;

/// <summary>为设备侧会话一次注册当前已实现的标准 Primary Message Handler。</summary>
public static class GemStandardHandlerSet
{
    public static GemPrimaryMessageRouter RegisterEquipmentHandlers(
        GemPrimaryMessageRouter router,
        GemRegistry registry,
        IGemEquipmentSession session,
        Func<IReadOnlyList<GemEquipmentConstantChange>, CancellationToken, Task<bool>> equipmentConstantWriter,
        Func<GemAlarmEnableRequest, CancellationToken, Task<bool>> alarmEnablementWriter,
        Func<CancellationToken, Task<bool>> onlineRequestAuthorizer,
        bool hostOnlineEntersRemote = false,
        IGemProcessProgramLoadPolicy? processProgramLoadPolicy = null,
        GemProcessProgramSaveOptions? processProgramSaveOptions = null)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(equipmentConstantWriter);
        ArgumentNullException.ThrowIfNull(alarmEnablementWriter);
        ArgumentNullException.ThrowIfNull(onlineRequestAuthorizer);
        processProgramLoadPolicy ??= new RejectGemProcessProgramLoadPolicy();

        return router
            .Register(new S1F11Handler(registry, session))
            .Register(new S1F13Handler(session))
            .Register(new S1F15Handler(session))
            .Register(new S1F17Handler(session, onlineRequestAuthorizer, hostOnlineEntersRemote))
            .Register(new S1F3Handler(registry))
            .Register(new S2F15Handler(registry, session, equipmentConstantWriter))
            .Register(new S2F23Handler(registry, session))
            .Register(new S2F41Handler(session))
            .Register(new S5F3Handler(registry, session, alarmEnablementWriter))
            .Register(new S7F1Handler(session, processProgramLoadPolicy))
            .Register(new S7F3Handler(session, processProgramSaveOptions))
            .Register(new S7F5Handler(session))
            .Register(new S7F17Handler(session))
            .Register(new S7F19Handler(session));
    }
}
