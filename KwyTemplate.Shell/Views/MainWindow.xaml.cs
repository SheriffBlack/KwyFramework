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
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmChar = 0x0102;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int ScMinimize = 0xF020;
    private const int ScMaximize = 0xF030;
    private const int ScRestore = 0xF120;
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
        // 扫码枪会同时产生 Raw Input 与普通键盘消息。扫码过程中的普通字符、
        // 末尾 Enter 以及 Alt 等系统键都不能继续路由给当前焦点控件；否则 Enter
        // 可能误执行“MES 连接”等按钮命令。
        if (rawInputBarcodeReceiver.ShouldSuppressKeyboardInput &&
            message is WmKeyDown or WmKeyUp or WmChar or WmSysKeyDown or WmSysKeyUp)
        {
            handled = true;
            logService.Warn($"Blocked MainWindow keyboard message during raw barcode scan: Message=0x{message:X4}, VirtualKey=0x{wParam.ToInt64() & 0xFFFF:X2}.");
            return IntPtr.Zero;
        }

        if (message == WmSysCommand)
        {
            int command = unchecked((int)(wParam.ToInt64() & SysCommandMask));
            string commandName = command switch
            {
                ScMinimize => "SC_MINIMIZE",
                ScMaximize => "SC_MAXIMIZE",
                ScRestore => "SC_RESTORE",
                _ => string.Empty
            };

            if (rawInputBarcodeReceiver.ShouldSuppressKeyboardInput &&
                (command == ScMinimize || command == ScMaximize || command == ScRestore))
            {
                handled = true;
                logService.Warn($"Blocked MainWindow WM_SYSCOMMAND during raw barcode scan: 0x{command:X4} ({commandName}).");
                return IntPtr.Zero;
            }

            logService.Info($"MainWindow WM_SYSCOMMAND: 0x{command:X4}" + (string.IsNullOrEmpty(commandName) ? string.Empty : $" ({commandName})"));
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
