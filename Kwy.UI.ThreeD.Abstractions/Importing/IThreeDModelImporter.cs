using Kwy.UI.ThreeD.Abstractions.Scene;

namespace Kwy.UI.ThreeD.Abstractions.Importing;

public interface IThreeDModelImporter
{
    IReadOnlySet<string> SupportedExtensions { get; }

    Task<SceneNodeDescriptor> ImportAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default);
}
