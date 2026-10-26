namespace Kwy.UI.ThreeD.Abstractions.Scene;

/// <summary>
/// 在视口创建或替换时，重建与渲染后端无关的场景。
/// </summary>
public interface IThreeDSceneSource
{
    void Populate(IThreeDScene scene);
}
