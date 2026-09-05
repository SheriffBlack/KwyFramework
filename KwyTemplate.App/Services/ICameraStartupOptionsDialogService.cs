using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

public interface ICameraStartupOptionsDialogService
{
    Task<CameraStartupOptions?> ShowAsync(bool isCameraAEnabled, bool isCameraBEnabled);
}
