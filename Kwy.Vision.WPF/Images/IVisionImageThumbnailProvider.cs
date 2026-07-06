using System.Windows.Media;

namespace Kwy.Vision.WPF.Images;

public interface IVisionImageThumbnailProvider
{
    ImageSource? CreateThumbnail(int maxWidth, int maxHeight);
}
