using Kwy.UI.WPF.Controls;
using KwyTemplate.App.Input;
using KwyTemplate.Contracts.Services;
using System.ComponentModel;
using System.Windows.Interop;

namespace KwyTemplate.Shell.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : KwyWindow
{
    private readonly IRawInputBarcodeReceiver rawInputBarcodeReceiver;
    private readonly IApplicationCloseGuard applicationCloseGuard;
    private bool isCloseApproved;
    private bool isCloseValidationInProgress;

    public MainWindow(
        IRawInputBarcodeReceiver rawInputBarcodeReceiver,
        IApplicationCloseGuard applicationCloseGuard)
    {
        this.rawInputBarcodeReceiver = rawInputBarcodeReceiver ?? throw new ArgumentNullException(nameof(rawInputBarcodeReceiver));
        this.applicationCloseGuard = applicationCloseGuard ?? throw new ArgumentNullException(nameof(applicationCloseGuard));
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        rawInputBarcodeReceiver.Initialize(new WindowInteropHelper(this).Handle);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SourceInitialized -= MainWindow_SourceInitialized;
        Closing -= MainWindow_Closing;
        Closed -= MainWindow_Closed;
        rawInputBarcodeReceiver.Dispose();
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (isCloseApproved)
        {
            return;
        }

        // Closing is synchronous. Always cancel this first attempt so the PLC
        // reset completes before WPF starts tearing down the application.
        e.Cancel = true;
        if (isCloseValidationInProgress)
        {
            return;
        }

        isCloseValidationInProgress = true;
        try
        {
            if (await applicationCloseGuard.CanCloseAsync())
            {
                isCloseApproved = true;
                _ = Dispatcher.BeginInvoke(new Action(Close));
            }
        }
        finally
        {
            isCloseValidationInProgress = false;
        }
    }
}
