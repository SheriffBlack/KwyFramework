using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using Kwy.UI.DataGrids;
using Kwy.UI.WPF.Controls.Helpers;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Services.FileDialogs;
using Kwy.MVVM.Messaging;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.App.ViewModels;

/// <summary>
/// 参数字典页面的列元数据。
/// </summary>
internal sealed class ParameterDictViewModel : BindableBase, INavigationAware
{
    private readonly ILocalizationService localizationService;
    private readonly ParameterDictOptionsStore optionsStore;
    private readonly IAppNotificationService notificationService;
    private readonly IInputDialogService inputDialogService;
    private readonly LocalWorkOrderRecipeMapper recipeMapper;
    private readonly LocalWorkOrderRecipeRuntimeSnapshotFactory recipeRuntimeSnapshotFactory;
    private readonly LocalWorkOrderRecipeStore recipeStore;
    private readonly ParameterDictSelectionSession selectionSession;
    private readonly IMessageBus messageBus;
    private readonly IDisposable localWorkOrderRecipeLoadRequestedSubscription;
    private readonly IFileDialogService fileDialogService;
    private readonly IWorkOrderSetupFileParser? workOrderSetupFileParser;
    private readonly ObservableCollection<IDataGridColumnDescriptor> columns = [];
    private readonly SemaphoreSlim reloadLock = new(1, 1);
    private static readonly TimeSpan FileSnapshotLifetime = TimeSpan.FromSeconds(10);
    private DateTimeOffset lastFileScanAt;
    private string? lastFileScanDirectory;
    private string queryFileName = string.Empty;
    private ParameterDictFileItemModel? selectedFile;
    private bool isShowingAllFiles = true;
    private AsyncDelegateCommand? deleteCommand;
    private AsyncDelegateCommand? newCommand;
    private AsyncDelegateCommand? importCommand;

    public ParameterDictViewModel(
        ILocalizationService localizationService,
        ParameterDictOptionsStore optionsStore,
        IAppNotificationService notificationService,
        IInputDialogService inputDialogService,
        LocalWorkOrderRecipeMapper recipeMapper,
        LocalWorkOrderRecipeRuntimeSnapshotFactory recipeRuntimeSnapshotFactory,
        LocalWorkOrderRecipeStore recipeStore,
        ParameterDictSelectionSession selectionSession,
        IMessageBus messageBus,
        IFileDialogService fileDialogService,
        IWorkOrderSetupFileParser? workOrderSetupFileParser = null)
    {
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.inputDialogService = inputDialogService ?? throw new ArgumentNullException(nameof(inputDialogService));
        this.recipeMapper = recipeMapper ?? throw new ArgumentNullException(nameof(recipeMapper));
        this.recipeRuntimeSnapshotFactory = recipeRuntimeSnapshotFactory ?? throw new ArgumentNullException(nameof(recipeRuntimeSnapshotFactory));
        this.recipeStore = recipeStore ?? throw new ArgumentNullException(nameof(recipeStore));
        this.recipeStore.RecipeSaved += OnRecipeSaved;
        this.selectionSession = selectionSession ?? throw new ArgumentNullException(nameof(selectionSession));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        this.fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        this.workOrderSetupFileParser = workOrderSetupFileParser;
        localWorkOrderRecipeLoadRequestedSubscription = this.messageBus.Subscribe<ParameterDictViewModel, LocalWorkOrderRecipeLoadRequestedMessage>(
            this,
            static (viewModel, message) =>
            {
                _ = message;
                _ = viewModel.LoadSelectedRecipeAsync();
            },
            MessageSubscribeOptions<LocalWorkOrderRecipeLoadRequestedMessage>.OnUI);
        SyncColumns();
        _ = ReloadFilesAsync(force: true);
        this.localizationService.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<IDataGridColumnDescriptor> Columns => columns;

    public ObservableCollection<ParameterDictFileItemModel> Files { get; } = [];

    private BulkObservableCollection<ParameterDictFileItemModel> visibleFiles = [];

    /// <summary>
    /// 旧参数字典一致的批量显示集合：筛选时整体替换，避免在 ComboBox
    /// 的选择事务中刷新 ICollectionView 而延迟文本回显或残留展开状态。
    /// </summary>
    public BulkObservableCollection<ParameterDictFileItemModel> VisibleFiles
    {
        get => visibleFiles;
        private set => SetProperty(ref visibleFiles, value);
    }

    public string QueryFileName
    {
        get => queryFileName;
        set
        {
            if (SetProperty(ref queryFileName, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public ParameterDictFileItemModel? SelectedFile
    {
        get => selectedFile;
        set => SetSelectedFile(value);
    }

    public AsyncDelegateCommand DeleteCommand
        => deleteCommand ??= new AsyncDelegateCommand(DeleteSelectedFileAsync, () => SelectedFile != null);

    public AsyncDelegateCommand NewCommand
        => newCommand ??= new AsyncDelegateCommand(CreateNewFileAsync);

    public AsyncDelegateCommand ImportCommand
        => importCommand ??= new AsyncDelegateCommand(ImportCustomerWorkOrderAsync, () => workOrderSetupFileParser != null);

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        // The region keeps this view cached.  Rebuilding both the DataGrid and
        // editable ComboBox collection on every tab return made this tab feel
        // noticeably slower than station parameter tabs.  File operations
        // already call force refresh themselves; only recover here if the
        // first asynchronous load has not supplied data yet.
        if (Files.Count == 0)
        {
            _ = ReloadFilesAsync();
        }
    }

    private void SyncColumns()
    {
        columns.Clear();
        columns.Add(CreateTextColumn(
            "FileName",
            localizationService.T("ParameterDict.Column.Name", "名称"),
            new DataGridLength(1, DataGridLengthUnitType.Star)));
        columns.Add(new WpfDataGridColumnOptions
        {
            ParameterId = "ModifyTime",
            DisplayName = localizationService.T("ParameterDict.Column.ModifiedTime", "修改时间"),
            BindingPath = "ModifyTime",
            StringFormat = "yyyy-MM-dd HH:mm",
            Width = new DataGridLength(400),
            ElementStyleKey = "DataGridCellTextBlockStyle",
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        });
    }

    private static IDataGridColumnDescriptor CreateTextColumn(string bindingPath, string displayName, DataGridLength width)
        => new WpfDataGridColumnOptions
        {
            ParameterId = bindingPath,
            DisplayName = displayName,
            BindingPath = bindingPath,
            Width = width,
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        };

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => SyncColumns();

    private void OnRecipeSaved(object? sender, LocalWorkOrderRecipeSavedEventArgs eventArgs)
    {
        // 保存可能来自 HomeView 的后台链路；始终回到 UI 线程更新绑定集合。
        _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            () => RefreshSavedFileModifyTime(eventArgs));
    }

    private void RefreshSavedFileModifyTime(LocalWorkOrderRecipeSavedEventArgs eventArgs)
    {
        int index = Files.ToList().FindIndex(file =>
            string.Equals(file.FullPath, eventArgs.FilePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        ParameterDictFileItemModel updated = Files[index] with { ModifyTime = eventArgs.ModifyTime };
        Files[index] = updated;
        for (int visibleIndex = 0; visibleIndex < VisibleFiles.Count; visibleIndex++)
        {
            if (string.Equals(VisibleFiles[visibleIndex].FullPath, eventArgs.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                VisibleFiles[visibleIndex] = updated;
            }
        }

        if (SelectedFile != null
            && string.Equals(SelectedFile.FullPath, eventArgs.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedFile = updated;
        }

        lastFileScanAt = DateTimeOffset.UtcNow;
    }

    private async Task ReloadFilesAsync(bool force = false)
    {
        ParameterDictOptions options = optionsStore.Current;
        string directoryPath = options.DirectoryPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        bool isSnapshotCurrent = !force
            && string.Equals(lastFileScanDirectory, directoryPath, StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.UtcNow - lastFileScanAt < FileSnapshotLifetime;
        if (isSnapshotCurrent)
        {
            return;
        }

        await reloadLock.WaitAsync().ConfigureAwait(true);
        try
        {
            isSnapshotCurrent = !force
                && string.Equals(lastFileScanDirectory, directoryPath, StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.UtcNow - lastFileScanAt < FileSnapshotLifetime;
            if (isSnapshotCurrent)
            {
                return;
            }

            IReadOnlyList<ParameterDictFileItemModel> scannedFiles = await Task.Run(
                () => ScanFiles(directoryPath, GetAllowedExtensions(options))).ConfigureAwait(true);
            string? selectedPath = SelectedFile?.FullPath;
            string? selectedFileName = SelectedFile?.FileName ?? selectionSession.SelectedFileName;

            Files.Clear();
            foreach (ParameterDictFileItemModel file in scannedFiles)
            {
                Files.Add(file);
            }
            isShowingAllFiles = false;
            lastFileScanDirectory = directoryPath;
            lastFileScanAt = DateTimeOffset.UtcNow;

            SelectedFile = string.IsNullOrWhiteSpace(selectedPath)
                ? Files.FirstOrDefault(file => string.Equals(file.FileName, selectedFileName, StringComparison.OrdinalIgnoreCase))
                : Files.FirstOrDefault(file => string.Equals(file.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
            if (SelectedFile != null)
            {
                queryFileName = SelectedFile.FileName;
                RaisePropertyChanged(nameof(QueryFileName));
            }

            ApplyFilter();
        }
        finally
        {
            reloadLock.Release();
        }
    }

    private static IReadOnlyList<ParameterDictFileItemModel> ScanFiles(string directoryPath, HashSet<string> allowedExtensions)
    {
        if (!Directory.Exists(directoryPath) || allowedExtensions.Count == 0)
        {
            return [];
        }

        try
        {
            return new DirectoryInfo(directoryPath)
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .Where(file => allowedExtensions.Contains(file.Extension))
                .OrderByDescending(static file => file.LastWriteTime)
                .ThenBy(static file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static file => new ParameterDictFileItemModel(
                    Path.GetFileNameWithoutExtension(file.Name),
                    file.LastWriteTime,
                    file.FullName))
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void ApplyFilter()
    {
        string query = queryFileName.Trim();
        ParameterDictFileItemModel? exactMatch = Files.FirstOrDefault(file =>
            string.Equals(file.FileName, query, StringComparison.OrdinalIgnoreCase));
        SelectedFile = exactMatch;

        // 选择已有工单或清空搜索框时，下拉框和左侧表格都应显示完整列表。
        // 这条路径不再重建 ItemsSource，避免 WPF 正在提交 ComboBox 选择时发生
        // CollectionChanged，导致选中文字回显缓慢、按钮仍保持下拉状态。
        if (string.IsNullOrWhiteSpace(query) || exactMatch != null)
        {
            if (!isShowingAllFiles)
            {
                VisibleFiles = new BulkObservableCollection<ParameterDictFileItemModel>(Files);
                isShowingAllFiles = true;
            }

            return;
        }

        VisibleFiles = new BulkObservableCollection<ParameterDictFileItemModel>(
            Files.Where(file => file.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)));
        isShowingAllFiles = false;
    }

    private bool SetSelectedFile(ParameterDictFileItemModel? value)
    {
        if (SetProperty(ref selectedFile, value))
        {
            selectionSession.SetSelectedFileName(value?.FileName);
            deleteCommand?.RaiseCanExecuteChanged();
            return true;
        }

        return false;
    }

    private async Task DeleteSelectedFileAsync()
    {
        ParameterDictFileItemModel? selected = SelectedFile;
        if (selected == null)
        {
            await notificationService.WarningAsync(
                localizationService.T("ParameterDict.Message.FileRequired", "请先选择要加载的本地工单配方。"),
                localizationService.T("ParameterDict.Title.Load", "加载参数字典"),
                writeLog: false).ConfigureAwait(true);
            return;
        }

        bool confirmed = await notificationService.ConfirmAsync(
            localizationService.TF(
                "ParameterDict.Message.DeleteConfirm",
                "是否确定删除“{0}”？",
                selected.FileName),
            localizationService.T("ParameterDict.Title.DeleteConfirm", "确认删除"),
            writeLog: false).ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        try
        {
            ValidateSelectedFile(selected);
            File.Delete(selected.FullPath);
            Files.Remove(selected);
            SelectedFile = null;
            ApplyFilter();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await notificationService.ErrorAsync(
                localizationService.TF(
                    "ParameterDict.Message.DeleteFailed",
                    "删除参数字典文件失败：{0}",
                    exception.Message),
                localizationService.T("ParameterDict.Title.DeleteFailed", "删除失败"),
                exception,
                writeLog: true).ConfigureAwait(true);
        }
    }

    private void ValidateSelectedFile(ParameterDictFileItemModel selected)
    {
        ParameterDictOptions options = optionsStore.Current;
        string configuredDirectory = Path.GetFullPath(options.DirectoryPath);
        string selectedPath = Path.GetFullPath(selected.FullPath);
        string relativePath = Path.GetRelativePath(configuredDirectory, selectedPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("The selected file is outside the configured directory.");
        }

        HashSet<string> allowedExtensions = GetAllowedExtensions(options);
        if (!allowedExtensions.Contains(Path.GetExtension(selectedPath)))
        {
            throw new ArgumentException("The selected file extension is not allowed.");
        }
    }

    private async Task CreateNewFileAsync()
    {
        InputDialogResult input = await inputDialogService.ShowAsync(new InputDialogOptions
        {
            Title = localizationService.T("ParameterDict.Title.New", "新建参数字典"),
            ShowContentTitle = false,
            Message = localizationService.T("ParameterDict.Message.NewMaterialNo", "请输入料号（文件名）。"),
            Label = localizationService.T("ParameterDict.Field.MaterialNo", "料号"),
            InputType = InputDialogType.Text,
            ConfirmButtonText = localizationService.T("Common.Confirm", "确定"),
            CancelButtonText = localizationService.T("Common.Cancel", "取消")
        }).ConfigureAwait(true);
        if (!input.IsConfirmed)
        {
            return;
        }

        string fileName;
        try
        {
            fileName = NormalizeNewFileName(input.Value);
        }
        catch (ArgumentException exception)
        {
            await notificationService.WarningAsync(
                localizationService.TF("ParameterDict.Message.InvalidFileName", "工单文件名无效：{0}", exception.Message),
                localizationService.T("ParameterDict.Title.New", "新建参数字典"),
                writeLog: false).ConfigureAwait(true);
            return;
        }

        ParameterDictOptions options = optionsStore.Current;
        HashSet<string> allowedExtensions = GetAllowedExtensions(options);
        string defaultExtension = NormalizeExtension(options.DefaultExtension);
        if (!allowedExtensions.Contains(defaultExtension))
        {
            await notificationService.WarningAsync(
                localizationService.T("ParameterDict.Message.DefaultExtensionInvalid", "默认新建文件格式未包含在允许格式中。"),
                localizationService.T("ParameterDict.Title.New", "新建参数字典"),
                writeLog: false).ConfigureAwait(true);
            return;
        }

        try
        {
            Directory.CreateDirectory(options.DirectoryPath);
            bool exists = Directory.EnumerateFiles(options.DirectoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => allowedExtensions.Contains(Path.GetExtension(path)))
                .Any(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileName, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                await notificationService.WarningAsync(
                    localizationService.TF("ParameterDict.Message.FileAlreadyExists", "工单“{0}”已存在，无法重复创建。", fileName),
                    localizationService.T("ParameterDict.Title.New", "新建参数字典"),
                    writeLog: false).ConfigureAwait(true);
                return;
            }

            string path = Path.Combine(options.DirectoryPath, $"{fileName}{defaultExtension}");
            if (string.Equals(defaultExtension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                // 参数字典文件以机种命名；新建时输入的名称就是机种标识。
                LocalWorkOrderRecipe recipe = recipeRuntimeSnapshotFactory.Create(fileName);
                await recipeStore.SaveAsync(recipe, path).ConfigureAwait(true);
                selectionSession.SetSelectedFileName(fileName);
                QueryFileName = fileName;
                await ReloadFilesAsync(force: true).ConfigureAwait(true);
                // 新建文件先进入 SetView 编辑，不修改 HomeView 工单，也不写入物理仪表。
                messageBus.Publish(new LocalWorkOrderRecipeEditLoadedMessage(
                    recipe,
                    recipeMapper.ToMesSetup(recipe),
                    path));
                return;
            }
            else
            {
                await File.WriteAllTextAsync(path, string.Empty).ConfigureAwait(true);
            }
            await ReloadFilesAsync(force: true).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await notificationService.ErrorAsync(
                localizationService.TF("ParameterDict.Message.CreateFailed", "新建参数字典文件失败：{0}", exception.Message),
                localizationService.T("ParameterDict.Title.New", "新建参数字典"),
                exception,
                writeLog: true).ConfigureAwait(true);
        }
    }

    private async Task LoadSelectedRecipeAsync()
    {
        ParameterDictFileItemModel? selected = SelectedFile;
        if (selected == null)
        {
            return;
        }

        if (!string.Equals(Path.GetExtension(selected.FullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            await notificationService.WarningAsync(
                localizationService.T("ParameterDict.Message.LoadJsonOnly", "本地工单配方仅支持加载 JSON 文件。"),
                localizationService.T("ParameterDict.Title.Load", "加载参数字典"),
                writeLog: false).ConfigureAwait(true);
            return;
        }

        try
        {
            ValidateSelectedFile(selected);
            LocalWorkOrderRecipe? recipe = recipeStore.LoadFromFile(selected.FullPath);
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.EquipmentType))
            {
                throw new InvalidDataException(localizationService.T("ParameterDict.Message.RecipeContentInvalid", "本地工单配方内容无效。"));
            }

            // SetView 顶部“应用”仅进入本地配方编辑，不修改 HomeView 工单字段，
            // 也不在此处写入仪表。
            messageBus.Publish(new LocalWorkOrderRecipeEditLoadedMessage(recipe, recipeMapper.ToMesSetup(recipe), selected.FullPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            await notificationService.ErrorAsync(
                localizationService.TF("ParameterDict.Message.LoadFailed", "加载参数字典失败：{0}", exception.Message),
                localizationService.T("ParameterDict.Title.Load", "加载参数字典"),
                exception,
                writeLog: true).ConfigureAwait(true);
        }
    }

    private async Task ImportCustomerWorkOrderAsync()
    {
        if (workOrderSetupFileParser == null)
        {
            await notificationService.WarningAsync(
                localizationService.T("ParameterDict.Message.ImportUnsupported", "当前 MES 未提供客户工单文件导入能力。"),
                localizationService.T("ParameterDict.Title.Import", "导入工单参数"),
                writeLog: false).ConfigureAwait(true);
            return;
        }

        string? sourcePath = fileDialogService.OpenFile(new OpenFileDialogOptions
        {
            Title = localizationService.T("ParameterDict.Title.Import", "导入工单参数"),
            Filter = localizationService.T("ParameterDict.FileFilter.CustomerWorkOrder", "工单参数文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"),
            CheckFileExists = true
        });
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        try
        {
            string sourceWorkOrderNo = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(sourceWorkOrderNo))
            {
                throw new InvalidDataException(localizationService.T("ParameterDict.Message.ImportFileNameInvalid", "无法从导入文件取得工单名称。"));
            }

            var setup = await Task.Run(
                () => workOrderSetupFileParser.Parse(sourceWorkOrderNo, sourcePath)).ConfigureAwait(true);
            LocalWorkOrderRecipe recipe = recipeMapper.FromMesSetup(setup, machineProfileKey: null, source: "Imported");
            string machineType = recipe.EquipmentType?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(machineType))
            {
                throw new InvalidDataException(localizationService.T("ParameterDict.Message.RecipeContentInvalid", "本地机种配方内容无效。"));
            }

            string targetFilePath = recipeStore.GetFilePath(machineType);
            await recipeStore.SaveAsync(recipe, targetFilePath).ConfigureAwait(true);

            await ReloadFilesAsync(force: true).ConfigureAwait(true);
            SelectedFile = Files.FirstOrDefault(file => string.Equals(file.FileName, machineType, StringComparison.OrdinalIgnoreCase));
            QueryFileName = machineType;
            // 导入与参数字典“应用”保持一致：仅进入 SetView 工位编辑，
            // 不修改 HomeView 工单字段，也不在此处写入物理仪表。
            messageBus.Publish(new LocalWorkOrderRecipeEditLoadedMessage(
                recipe,
                recipeMapper.ToMesSetup(recipe),
                targetFilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or FormatException)
        {
            await notificationService.ErrorAsync(
                localizationService.TF("ParameterDict.Message.ImportFailed", "导入工单参数失败：{0}", exception.Message),
                localizationService.T("ParameterDict.Title.Import", "导入工单参数"),
                exception,
                writeLog: true).ConfigureAwait(true);
        }
    }

    private string NormalizeNewFileName(string value)
    {
        string fileName = value.Trim();
        foreach (string extension in GetAllowedExtensions(optionsStore.Current))
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^extension.Length];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(localizationService.T("ParameterDict.Message.FileNameRule", "不能包含路径或非法字符。"));
        }

        return fileName;
    }

    private static HashSet<string> GetAllowedExtensions(ParameterDictOptions options)
        => options.AllowedExtensions
            .Where(static extension => !string.IsNullOrWhiteSpace(extension))
            .Select(NormalizeExtension)
            .Where(static extension => extension is ".json" or ".txt")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeExtension(string extension)
    {
        string trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
            recipeStore.RecipeSaved -= OnRecipeSaved;
            localWorkOrderRecipeLoadRequestedSubscription.Dispose();
        }

        base.Dispose(disposing);
    }
}
