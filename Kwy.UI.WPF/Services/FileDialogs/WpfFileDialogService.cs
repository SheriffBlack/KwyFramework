using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// WPF implementation of <see cref="IFileDialogService"/>.
/// </summary>
public sealed class WpfFileDialogService : IFileDialogService
{
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
            Filter = options.Filter,
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
            Filter = options.Filter,
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

    private static string ResolveDefaultExtension(FileDialogOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DefaultExtension))
        {
            return NormalizeExtension(options.DefaultExtension);
        }

        var parts = options.Filter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
