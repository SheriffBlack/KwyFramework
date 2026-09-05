namespace KwyTemplate.App.Models;

/// <summary>启动前由操作员确认的相机工位启用状态。</summary>
public sealed record CameraStartupOptions(bool IsCameraAEnabled, bool IsCameraBEnabled);
