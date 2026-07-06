using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Kwy.Files.Excel.Interop.Interop;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropApplication : IAsyncDisposable, IDisposable
{
    private static readonly ConcurrentBag<int> ManagedProcessIds = new();

    private readonly ExcelInteropOptions options;
    private readonly IExcelInteropEnvironment environment;
    private readonly ExcelInteropActionQueue queue;
    private OfficeExcel.Application? application;
    private bool disposed;

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    public ExcelInteropApplication(ExcelInteropOptions options, IExcelInteropEnvironment environment)
    {
        this.options = options;
        this.environment = environment;
        queue = new ExcelInteropActionQueue(options);
    }

    public Task RunAsync(Action<OfficeExcel.Application> action, CancellationToken cancellationToken = default)
    {
        return queue.RunAsync(() => action(GetOrCreateApplication()), cancellationToken);
    }

    public Task<T> RunAsync<T>(Func<OfficeExcel.Application, T> action, CancellationToken cancellationToken = default)
    {
        return queue.RunAsync(() => action(GetOrCreateApplication()), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            queue.RunAsync(CleanupApplication).GetAwaiter().GetResult();
        }
        catch
        {
        }

        queue.Dispose();

        if (options.KillOwnedExcelProcessOnDispose)
        {
            KillManagedExcelProcesses();
        }
    }

    private OfficeExcel.Application GetOrCreateApplication()
    {
        if (application != null)
        {
            try
            {
                _ = application.Version;
                return application;
            }
            catch
            {
                application = null;
            }
        }

        environment.EnsureExcelInstalled();
        OleMessageFilter.Register();

        application = new OfficeExcel.Application();
        TrySet(() => application.Visible = options.Visible);
        TrySet(() => application.DisplayAlerts = options.DisplayAlerts);
        TrySet(() => application.ScreenUpdating = options.ScreenUpdating);
        TrySet(() => application.Calculation = OfficeExcel.XlCalculation.xlCalculationManual);

        TrySet(() =>
        {
            GetWindowThreadProcessId(new IntPtr(application.Hwnd), out int processId);
            if (processId > 0)
            {
                ManagedProcessIds.Add(processId);
            }
        });

        return application;
    }

    private void CleanupApplication()
    {
        try
        {
            OleMessageFilter.Revoke();
        }
        catch
        {
        }

        if (application == null)
        {
            return;
        }

        try
        {
            application.Quit();
        }
        catch
        {
        }

        ComObject.Release(application);
        application = null;
    }

    private static void TrySet(Action action)
    {
        try
        {
            action();
        }
        catch
        {
        }
    }

    private static void KillManagedExcelProcesses()
    {
        foreach (int processId in ManagedProcessIds)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (!process.HasExited && process.ProcessName.Contains("EXCEL", StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }
    }
}
