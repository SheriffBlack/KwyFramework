using System.Windows;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// 弹窗容器的接口抽象。如果你希望用完全定制的无边框窗口作为容器，你可以自定义一个 Window 实现此接口并在服务中注册。
/// </summary>
public interface IDialogWindow
{
    object? DataContext { get; set; }
    object? Content { get; set; }
    Window? Owner { get; set; }
    bool? DialogResult { get; set; }
    string Title { get; set; }

    void Show();

    bool? ShowDialog();

    void Close();
}

/// <summary>
/// 默认的 WPF 版对话框窗口实现。只作为弹窗 View 的容器壳子。
/// 没有任何自身的界面逻辑，Content 全靠你传入的 UserControl 视填充。
/// </summary>
public class DefaultDialogWindow : Window, IDialogWindow
{
    public DefaultDialogWindow()
    {
        // 弹窗的默认显示行为
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinHeight = 150;
        MinWidth = 250;

        // （可选）可以根据需求调整比如不允许改变大小：
        // ResizeMode = ResizeMode.NoResize;
    }
}
