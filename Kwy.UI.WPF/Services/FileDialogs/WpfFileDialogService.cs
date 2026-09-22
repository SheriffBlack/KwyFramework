using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Kwy.UI.Services.FileDialogs;

namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// <see cref="IFileDialogService"/> 的 WPF 实现。
/// </summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    private const string ThisPcShellNamespace = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private readonly object syncRoot = new();
    private string? lastDirectory;

    public string? OpenFile(OpenFileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var singleFileOptions = new OpenFileDialogOptions
        {
            Title = options.Title,
            Filter = options.Filter,
            InitialDirectory = options.InitialDirectory,
            FileName = options.FileName,
            DefaultExtension = options.DefaultExtension,
            CheckFileExists = options.CheckFileExists,
            Multiselect = false
        };

        return OpenFiles(singleFileOptions).FirstOrDefault();
    }

    public IReadOnlyList<string> OpenFiles(OpenFileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dialog = new OpenFileDialog
        {
            Title = options.Title,
            Filter = options.Filter ?? string.Empty,
            FileName = options.FileName ?? string.Empty,
            DefaultExt = ResolveDefaultExtension(options),
            InitialDirectory = GetValidInitialDirectory(options.InitialDirectory),
            Multiselect = options.Multiselect,
            CheckFileExists = options.CheckFileExists,
            CheckPathExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return Array.Empty<string>();
        }

        RememberDirectory(dialog.FileName);
        return dialog.FileNames;
    }

    public string? SaveFile(SaveFileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dialog = new SaveFileDialog
        {
            Title = options.Title,
            Filter = options.Filter ?? string.Empty,
            FileName = options.FileName ?? string.Empty,
            DefaultExt = ResolveDefaultExtension(options),
            InitialDirectory = GetValidInitialDirectory(options.InitialDirectory),
            AddExtension = options.AddExtension,
            OverwritePrompt = options.OverwritePrompt,
            CheckPathExists = true
        };

        if (ShowDialog(dialog) != true)
        {
            return null;
        }

        RememberDirectory(dialog.FileName);
        return dialog.FileName;
    }

    public string? SelectFolder(FolderDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dialog = new OpenFolderDialog
        {
            Title = options.Title,
            InitialDirectory = GetValidInitialDirectory(options.InitialDirectory)
        };

        if (ShowDialog(dialog) != true)
        {
            return null;
        }

        RememberDirectory(dialog.FolderName);
        return dialog.FolderName;
    }

    /// <inheritdoc />
    public bool OpenInFileExplorer(string? path = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return StartExplorer(ThisPcShellNamespace);
            }

            string fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return StartExplorer(fullPath);
            }

            if (File.Exists(fullPath))
            {
                return StartExplorer($"/select,\"{fullPath}\"");
            }
        }
        catch (ArgumentException)
        {
            // 传入的路径不是有效的 Windows 路径。
        }
        catch (IOException)
        {
            // 路径无效或驱动器不可访问，资源管理器无法打开。
        }
        catch (UnauthorizedAccessException)
        {
            // 当前用户无权访问该路径，无法启动资源管理器。
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 当前 Windows 环境中无法使用资源管理器。
        }

        return false;
    }

    private string GetValidInitialDirectory(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            return path!;
        }

        lock (syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(lastDirectory) && Directory.Exists(lastDirectory))
            {
                return lastDirectory!;
            }
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents)
            ? documents
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private void RememberDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        lock (syncRoot)
        {
            lastDirectory = directory;
        }
    }

    private static bool? ShowDialog(CommonDialog dialog)
    {
        var owner = ResolveActiveWindow();
        return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }

    private static bool StartExplorer(string arguments)
        => Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        }) != null;

    private static string ResolveDefaultExtension(FileDialogOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DefaultExtension))
        {
            return NormalizeExtension(options.DefaultExtension);
        }

        var parts = (options.Filter ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        var extension = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return NormalizeExtension(extension);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        return extension.Replace("*", string.Empty, StringComparison.Ordinal).Trim().TrimStart('.');
    }

    private static Window? ResolveActiveWindow()
    {
        if (Application.Current == null)
        {
            return null;
        }

        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current.MainWindow;
    }
}
