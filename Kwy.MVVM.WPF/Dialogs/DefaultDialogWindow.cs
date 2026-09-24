using System.Windows;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// 弹窗容器接口。实现类型必须继承 <see cref="Window"/>，可使用自定义无边框窗口替换默认实现。
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
        ShowInTaskbar = false;
        ShowActivated = true;

        // （可选）可以根据需求调整比如不允许改变大小：
        // ResizeMode = ResizeMode.NoResize;
    }
}
