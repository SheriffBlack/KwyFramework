using Kwy.UI.WPF.Controls;
using Kwy.UI.WPF.Components.Logging;
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
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;
    private const int SysCommandMask = 0xFFF0;
    private readonly IRawInputBarcodeReceiver rawInputBarcodeReceiver;
    private readonly IApplicationCloseGuard applicationCloseGuard;
    private readonly KwyLogService logService;
    private HwndSource? hwndSource;
    private bool isCloseApproved;
    private bool isCloseValidationInProgress;

    public MainWindow(
        IRawInputBarcodeReceiver rawInputBarcodeReceiver,
        IApplicationCloseGuard applicationCloseGuard,
        KwyLogService logService)
    {
        this.rawInputBarcodeReceiver = rawInputBarcodeReceiver ?? throw new ArgumentNullException(nameof(rawInputBarcodeReceiver));
        this.applicationCloseGuard = applicationCloseGuard ?? throw new ArgumentNullException(nameof(applicationCloseGuard));
        this.logService = logService ?? throw new ArgumentNullException(nameof(logService));
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        rawInputBarcodeReceiver.DiagnosticOccurred += RawInputBarcodeReceiver_DiagnosticOccurred;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        hwndSource = HwndSource.FromHwnd(hwnd);
        hwndSource?.AddHook(MainWindowWndProc);
        rawInputBarcodeReceiver.Initialize(hwnd);
    }

    private IntPtr MainWindowWndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmSysCommand)
        {
            int command = unchecked((int)(wParam.ToInt64() & SysCommandMask));
            logService.Info($"MainWindow WM_SYSCOMMAND: 0x{command:X4}" + (command == ScMinimize ? " (SC_MINIMIZE)" : string.Empty));
        }

        return IntPtr.Zero;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
        => logService.Info($"MainWindow.StateChanged: WindowState={WindowState}");

    private void RawInputBarcodeReceiver_DiagnosticOccurred(object? sender, RawInputBarcodeDiagnosticEventArgs e)
    {
        string message = e.Kind switch
        {
            RawInputBarcodeDiagnosticKind.ScanStarted => "Raw barcode scan started.",
            RawInputBarcodeDiagnosticKind.KeyReceived => $"Raw barcode key: VirtualKey=0x{e.VirtualKey ?? 0:X2}, Shift={e.IsShiftPressed}, BufferLength={e.BufferLength}.",
            RawInputBarcodeDiagnosticKind.ScanCompleted => $"Raw barcode scan completed: BufferLength={e.BufferLength}, Code={e.Code}.",
            _ => "Raw barcode diagnostic event received."
        };

        logService.Info(message);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SourceInitialized -= MainWindow_SourceInitialized;
        StateChanged -= MainWindow_StateChanged;
        Closing -= MainWindow_Closing;
        Closed -= MainWindow_Closed;
        rawInputBarcodeReceiver.DiagnosticOccurred -= RawInputBarcodeReceiver_DiagnosticOccurred;
        if (hwndSource is not null)
        {
            hwndSource.RemoveHook(MainWindowWndProc);
            hwndSource = null;
        }
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
