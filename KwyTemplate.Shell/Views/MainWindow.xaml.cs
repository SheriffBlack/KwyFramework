using Kwy.UI.WPF.Controls;
using KwyTemplate.App.Input;
using System.Windows.Interop;

namespace KwyTemplate.Shell.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : KwyWindow
{
    private readonly IRawInputBarcodeReceiver rawInputBarcodeReceiver;

    public MainWindow(IRawInputBarcodeReceiver rawInputBarcodeReceiver)
    {
        this.rawInputBarcodeReceiver = rawInputBarcodeReceiver ?? throw new ArgumentNullException(nameof(rawInputBarcodeReceiver));
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        rawInputBarcodeReceiver.Initialize(new WindowInteropHelper(this).Handle);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SourceInitialized -= MainWindow_SourceInitialized;
        Closed -= MainWindow_Closed;
        rawInputBarcodeReceiver.Dispose();
    }
}