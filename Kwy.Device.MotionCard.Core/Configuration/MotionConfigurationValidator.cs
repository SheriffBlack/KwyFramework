using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Abstractions.Configuration;
using Kwy.Device.MotionCard.Abstractions.Controller;
using Kwy.Device.MotionCard.Abstractions.Groups;
using Kwy.Device.MotionCard.Abstractions.Synchronization;

namespace Kwy.Device.MotionCard.Core.Configuration;

/// <summary>跨卡、跨轴、跨运动组的启动期配置一致性校验。</summary>
public sealed class MotionConfigurationValidator : IMotionConfigurationValidator
{
    private readonly IMotionRuntimeRegistry runtimes;
    private readonly IAxisDefinitionProvider axisDefinitions;
    private readonly IMotionGroupDefinitionProvider groups;
    private readonly IIoPointDefinitionProvider? ioPointDefinitions;
    private readonly IVirtualAxisDefinitionProvider? virtualAxes;
    private readonly IMotionSynchronizationDefinitionProvider? synchronizations;

    public MotionConfigurationValidator(
        IMotionRuntimeRegistry runtimes,
        IAxisDefinitionProvider axisDefinitions,
        IMotionGroupDefinitionProvider groups,
        IIoPointDefinitionProvider? ioPointDefinitions = null,
        IVirtualAxisDefinitionProvider? virtualAxes = null,
        IMotionSynchronizationDefinitionProvider? synchronizations = null)
    {
        this.runtimes = runtimes;
        this.axisDefinitions = axisDefinitions;
        this.groups = groups;
        this.ioPointDefinitions = ioPointDefinitions;
        this.virtualAxes = virtualAxes;
        this.synchronizations = synchronizations;
    }

    public MotionConfigurationValidationResult Validate()
    {
        var issues = new List<MotionConfigurationIssue>();
        AxisDefinition[] axes = axisDefinitions.Definitions.ToArray();
        VirtualAxisDefinition[] virtualAxisDefinitions = virtualAxes?.VirtualAxes.ToArray() ?? [];
        AxisResourceDefinition[] resources = axes.Cast<AxisResourceDefinition>().Concat(virtualAxisDefinitions).ToArray();
        AddDuplicate(resources.Select(axis => axis.Id), "AxisIdDuplicate", "Business axis ID", issues);
        AddDuplicate(axes.Select(axis => $"{axis.DeviceId}:{axis.Channel}"), "AxisChannelDuplicate", "Physical axis channel", issues);
        foreach (AxisDefinition axis in axes)
        {
            try { axis.Validate(); } catch (Exception ex) { Error("AxisInvalid", $"Axis '{axis.Id}': {ex.Message}"); }
            IMotionDeviceRuntime? axisRuntime = runtimes.Runtimes.SingleOrDefault(runtime => string.Equals(runtime.DeviceId, axis.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (axisRuntime is null)
            {
                Error("AxisDeviceMissing", $"Axis '{axis.Id}' references missing device '{axis.DeviceId}'.");
            }
            else if (axisRuntime.Card is not IAxisChannelDefinitionProvider channels)
            {
                Error("AxisChannelProviderMissing", $"Motion device '{axis.DeviceId}' does not expose axis channel definitions.");
            }
            else
            {
                try { _ = channels.GetAxisDefinition(axis.Channel); }
                catch (Exception) { Error("AxisChannelMissing", $"Axis '{axis.Id}' references unavailable channel '{axis.Channel}' on device '{axis.DeviceId}'."); }
            }
            foreach (string interlock in axis.Safety.RequiredInterlocks)
                if (ioPointDefinitions is null || !ioPointDefinitions.TryGet(interlock, out _))
                    Error("InterlockPointMissing", $"Axis '{axis.Id}' references missing IO interlock '{interlock}'.");
        }
        foreach (VirtualAxisDefinition axis in virtualAxisDefinitions)
        {
            try { axis.Validate(); } catch (Exception ex) { Error("VirtualAxisInvalid", $"Virtual axis '{axis.Id}': {ex.Message}"); }
            IMotionDeviceRuntime? runtime = runtimes.Runtimes.SingleOrDefault(item => string.Equals(item.DeviceId, axis.HostDeviceId, StringComparison.OrdinalIgnoreCase));
            if (runtime is null)
                Error("VirtualAxisHostMissing", $"Virtual axis '{axis.Id}' references missing host device '{axis.HostDeviceId}'.");
            else if (runtime.Card is not IMotionControllerCapabilities capabilities || !capabilities.Capabilities.SupportsControllerVirtualAxis)
                Error("VirtualAxisCapability", $"Motion device '{axis.HostDeviceId}' does not provide a controller-hosted virtual axis.");
        }
        AddDuplicate(groups.MotionGroups.Select(group => $"{group.DeviceId}:{group.CoordinateSystemChannel}"), "CoordinateDuplicate", "Coordinate-system channel", issues);
        foreach (MotionGroupDefinition group in groups.MotionGroups)
        {
            try { group.Validate(); } catch (Exception ex) { Error("MotionGroupInvalid", $"Group '{group.Id}': {ex.Message}"); continue; }
            foreach (string axisId in group.AxisIds)
            {
                AxisDefinition? axis = axes.SingleOrDefault(item => string.Equals(item.Id, axisId, StringComparison.OrdinalIgnoreCase));
                if (axis is null) Error("MotionGroupAxisMissing", $"Group '{group.Id}' references missing axis '{axisId}'.");
                else if (!string.Equals(axis.DeviceId, group.DeviceId, StringComparison.OrdinalIgnoreCase)) Error("MotionGroupCrossDevice", $"Group '{group.Id}' contains axis '{axisId}' from device '{axis.DeviceId}'.");
                else if (!axis.Capabilities.HasFlag(AxisMotionCapability.Interpolation)) Error("MotionGroupAxisCapability", $"Axis '{axisId}' is not enabled for interpolation.");
            }
        }
        if (synchronizations is not null)
        {
            AddDuplicate(synchronizations.ElectronicGears.Select(item => item.Id).Concat(synchronizations.ElectronicCams.Select(item => item.Id)), "SynchronizationIdDuplicate", "Synchronization ID", issues);
            foreach (ElectronicGearDefinition gear in synchronizations.ElectronicGears)
            {
                try { gear.Validate(); } catch (Exception ex) { Error("ElectronicGearInvalid", $"Electronic gear '{gear.Id}': {ex.Message}"); continue; }
                ValidateSynchronizationAxes(gear.Id, gear.MasterAxisId, gear.SlaveAxisId, "electronic gear");
            }
            foreach (ElectronicCamDefinition cam in synchronizations.ElectronicCams)
            {
                try { cam.Validate(); } catch (Exception ex) { Error("ElectronicCamInvalid", $"Electronic cam '{cam.Id}': {ex.Message}"); continue; }
                ValidateSynchronizationAxes(cam.Id, cam.MasterAxisId, cam.FollowerAxisId, "electronic cam");
            }
        }
        return new MotionConfigurationValidationResult(issues);

        void Error(string code, string message) => issues.Add(new(MotionConfigurationIssueSeverity.Error, code, message));

        void ValidateSynchronizationAxes(string relationshipId, string masterAxisId, string followerAxisId, string relationship)
        {
            AxisResourceDefinition? master = resources.SingleOrDefault(item => string.Equals(item.Id, masterAxisId, StringComparison.OrdinalIgnoreCase));
            AxisResourceDefinition? follower = resources.SingleOrDefault(item => string.Equals(item.Id, followerAxisId, StringComparison.OrdinalIgnoreCase));
            if (master is null) Error("SynchronizationMasterMissing", $"{relationship} '{relationshipId}' references missing master axis '{masterAxisId}'.");
            else if (!master.Capabilities.HasFlag(AxisMotionCapability.SynchronizationMaster)) Error("SynchronizationMasterCapability", $"Axis '{masterAxisId}' is not enabled as a synchronization master.");
            if (follower is null) Error("SynchronizationFollowerMissing", $"{relationship} '{relationshipId}' references missing follower axis '{followerAxisId}'.");
            else if (!follower.Capabilities.HasFlag(AxisMotionCapability.SynchronizationFollower)) Error("SynchronizationFollowerCapability", $"Axis '{followerAxisId}' is not enabled as a synchronization follower.");
            if (master is not null && follower is not null)
            {
                string masterDeviceId = GetHostDeviceId(master);
                string followerDeviceId = GetHostDeviceId(follower);
                if (!string.Equals(masterDeviceId, followerDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    Error("SynchronizationCrossDevice", $"{relationship} '{relationshipId}' spans '{masterDeviceId}' and '{followerDeviceId}', but real-time synchronization must be hosted by one controller.");
                }
                else
                {
                    IMotionDeviceRuntime? runtime = runtimes.Runtimes.SingleOrDefault(item => string.Equals(item.DeviceId, masterDeviceId, StringComparison.OrdinalIgnoreCase));
                    bool supported = runtime?.Card is IMotionControllerCapabilities capabilities
                        && (relationship == "electronic gear"
                            ? capabilities.Capabilities.SupportsNativeElectronicGear
                            : capabilities.Capabilities.SupportsNativeElectronicCam);
                    if (!supported)
                        Error("SynchronizationControllerCapability", $"Motion device '{masterDeviceId}' does not provide native {relationship} capability.");
                }
            }
        }

        static string GetHostDeviceId(AxisResourceDefinition resource) => resource switch
        {
            AxisDefinition axis => axis.DeviceId,
            VirtualAxisDefinition axis => axis.HostDeviceId,
            _ => throw new NotSupportedException($"Unsupported axis resource '{resource.GetType().Name}'.")
        };
    }

    public void ValidateAndThrow() { MotionConfigurationValidationResult result = Validate(); if (!result.IsValid) throw new MotionConfigurationException(result); }

    private static void AddDuplicate(IEnumerable<string> values, string code, string label, List<MotionConfigurationIssue> issues)
    {
        foreach (string duplicate in values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
            issues.Add(new(MotionConfigurationIssueSeverity.Error, code, $"{label} '{duplicate}' is configured more than once."));
    }
}
