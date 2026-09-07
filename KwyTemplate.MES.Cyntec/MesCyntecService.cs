using cyntec.TcpTools;
using KwyTemplate.MES.Abstract.Events;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.MES.Cyntec;

public sealed class MesCyntecService :
    IMesConnection,
    IMesWorkOrderService,
    IMesTrackService,
    IMesStandardSampleService,
    IMesReelService
{
    private readonly CyntecMesOptions options;
    private readonly tcpSocket socket;
    private readonly CyntecMesApiLogger apiLogger;
    private MesConnectionState state = MesConnectionState.Offline;

    public MesCyntecService()
        : this(new CyntecMesOptions())
    {
    }

    public MesCyntecService(CyntecMesOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        apiLogger = new CyntecMesApiLogger(this.options);
        mesAPI.IsJsonFormat = false;
        socket = new tcpSocket
        {
            IP = options.IpAddress,
            port = options.Port,
            ReadTimeout = options.ReadTimeout,
            WriteTimeout = options.WriteTimeout
        };
    }

    public MesConnectionState State => state;

    public event EventHandler<MesStateChangedEventArgs>? StateChanged;

    public Task<MesResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(MesConnectionState.Connecting, "Connecting Cyntec MES.");

        var api = new mesAPIconnect
        {
            txnid = mesAPI.NewTxnID(string.Empty)
        };

        string raw = Send(api);
        MesExchangeRecord exchange = CreateExchange("connect", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw);
        if (api.returncode == 0)
        {
            SetState(MesConnectionState.Online, api.returnmessage);
            return Task.FromResult(MesResult.Ok(api.returnmessage, exchange));
        }

        SetState(MesConnectionState.Faulted, api.returnmessage);
        return Task.FromResult(MesResult.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
    }

    public Task<MesResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(MesConnectionState.Offline, "Disconnected Cyntec MES.");
        return Task.FromResult(MesResult.Ok("Disconnected."));
    }

    public Task<MesResult<MesWorkOrderSetup>> GetWorkOrderSetupAsync(MesWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var api = new mesAPIwoQuery
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            workorderno = request.WorkOrderNo,
            badgeno = request.Context.OperatorId,
            equipmentid = request.Context.MachineId,
            tpno = GetExtra(request.Context, "TpNo"),
            matno = request.Context.ProductNo ?? GetExtra(request.Context, "MatNo")
        };

        string raw = Send(api);
        string setupFile = BuildMesFilePath(options.SetupDirectory, request.WorkOrderNo, options.SetupFileExtension);
        MesExternalDataSource source = CreateFileSource(setupFile, "key-value-csv");
        MesExchangeRecord exchange = CreateExchange("woquery", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw, source);

        if (api.returncode != 0)
        {
            return Task.FromResult(MesResult<MesWorkOrderSetup>.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
        }

        if (!File.Exists(setupFile))
        {
            return Task.FromResult(MesResult<MesWorkOrderSetup>.Fail(MesResultCodes.DataNotFound, $"MES setup file was not found: {setupFile}", exchange: exchange));
        }

        try
        {
            MesWorkOrderSetup setup = CyntecMesFileParser.ParseWorkOrderSetup(request.WorkOrderNo, setupFile);
            if (!string.IsNullOrWhiteSpace(api.partnumber))
            {
                setup = setup with { EquipmentType = api.partnumber };
            }

            return Task.FromResult(MesResult<MesWorkOrderSetup>.Ok(setup, api.returnmessage, exchange));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MesResult<MesWorkOrderSetup>.Fail(MesResultCodes.DataParseFailed, $"Failed to parse MES setup file: {setupFile}", ex.Message, exchange));
        }
    }

    public Task<MesResult<MesTrackResult>> TrackInAsync(MesTrackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        MesResult archiveResult = ArchiveProgramSetup(request.WorkOrderNo);
        if (!archiveResult.IsSuccess)
        {
            return Task.FromResult(MesResult<MesTrackResult>.Fail(
                archiveResult.Code,
                archiveResult.Message,
                archiveResult.Detail));
        }

        var api = new mesAPIcheckIn
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            workorderno = request.WorkOrderNo,
            badgeno = request.Context.OperatorId,
            equipmentid = request.Context.MachineId
        };

        string raw = Send(api);
        MesExchangeRecord exchange = CreateExchange("checkin", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw);
        if (api.returncode == 0)
        {
            var result = new MesTrackResult(true, api.returnmessage);
            return Task.FromResult(MesResult<MesTrackResult>.Ok(result, api.returnmessage, exchange));
        }

        return Task.FromResult(MesResult<MesTrackResult>.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
    }
    private MesResult ArchiveProgramSetup(string workOrderNo, MesExchangeRecord? exchange = null)
    {
        string sourceFile = BuildMesFilePath(options.SetupDirectory, workOrderNo, options.SetupFileExtension);
        string targetFile = BuildMesFilePath(options.ProgramDirectory, workOrderNo, options.SetupFileExtension);

        if (!File.Exists(sourceFile))
        {
            return MesResult.Fail(
                MesResultCodes.DataNotFound,
                $"MES setup file was not found: {sourceFile}",
                exchange: exchange);
        }

        try
        {
            Directory.CreateDirectory(options.ProgramDirectory);
            File.Copy(sourceFile, targetFile, overwrite: true);
            return MesResult.Ok($"MES program setup archived: {targetFile}", exchange);
        }
        catch (Exception ex)
        {
            return MesResult.Fail(
                MesResultCodes.DataWriteFailed,
                $"Failed to archive MES program setup: {targetFile}",
                ex.Message,
                exchange);
        }
    }
    public Task<MesResult<MesTrackResult>> TrackOutAsync(MesTrackOutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var api = new mesAPIcheckOut
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            workorderno = request.WorkOrderNo,
            badgeno = request.Context.OperatorId,
            equipmentid = request.Context.MachineId,
            outputquantity = request.OutputQuantity ?? request.Measurements.Count
        };

        string raw = Send(api);
        MesExchangeRecord exchange = CreateExchange("checkout", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw);
        if (api.returncode == 0)
        {
            var result = new MesTrackResult(true, api.returnmessage);
            return Task.FromResult(MesResult<MesTrackResult>.Ok(result, api.returnmessage, exchange));
        }

        return Task.FromResult(MesResult<MesTrackResult>.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
    }

    public Task<MesResult<MesStandardSampleSetup>> GetStandardSampleAsync(MesStandardSampleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string sampleCode = request.SampleCode ?? string.Empty;
        var api = new mesAPISTDPartsQuery
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            stdpartsid = sampleCode,
            equipmentid = request.Context.MachineId,
            workorderno = request.WorkOrderNo
        };

        string raw = Send(api);
        string stdFile = BuildMesFilePath(options.StandardPartDirectory, sampleCode, options.StandardPartFileExtension);
        MesExternalDataSource source = CreateFileSource(stdFile, "standard-parts-csv");
        MesExchangeRecord exchange = CreateExchange("stdpartsquery", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw, source);

        if (api.returncode != 0)
        {
            return Task.FromResult(MesResult<MesStandardSampleSetup>.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
        }

        if (!File.Exists(stdFile))
        {
            return Task.FromResult(MesResult<MesStandardSampleSetup>.Fail(MesResultCodes.DataNotFound, $"MES standard part file was not found: {stdFile}", exchange: exchange));
        }

        try
        {
            MesStandardSampleSetup setup = CyntecMesFileParser.ParseStandardSampleSetup(request.WorkOrderNo, sampleCode, stdFile);
            return Task.FromResult(MesResult<MesStandardSampleSetup>.Ok(setup, api.returnmessage, exchange));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MesResult<MesStandardSampleSetup>.Fail(MesResultCodes.DataParseFailed, $"Failed to parse MES standard part file: {stdFile}", ex.Message, exchange));
        }
    }

    /// <summary>
    /// 本地设备点检文件保存，不调用 MES API
    /// 路径 D:\MES\Equipment
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<MesResult> SaveStandardSampleCheckEquipmentAsync(MesStandardSampleCheckSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string equipmentFile = BuildMesFilePath(options.EquipmentDirectory, request.Context.MachineId, options.EquipmentFileExtension);
        try
        {
            CyntecStandardSampleCheckFileWriter.Write(equipmentFile, request);
            return Task.FromResult(MesResult.Ok($"Standard sample check file saved: {equipmentFile}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MesResult.Fail(MesResultCodes.DataWriteFailed, $"Failed to write MES standard check file: {equipmentFile}", ex.Message));
        }
    }

    public Task<MesResult> SaveStandardSampleCheckAsync(MesStandardSampleCheckSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var api = new mesStdPartsCheckResultSave
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            badgeno = request.Context.OperatorId,
            equipmentid = request.Context.MachineId,
            workorderno = request.WorkOrderNo
        };

        string equipmentFile = BuildMesFilePath(options.EquipmentDirectory, request.Context.MachineId, options.EquipmentFileExtension);
        MesExternalDataSource source = CreateFileSource(equipmentFile, "standard-check-result-csv");
        string raw = Send(api);
        MesExchangeRecord exchange = CreateExchange("stdpartscheckresultsave", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw, source);
        return api.returncode == 0
            ? Task.FromResult(MesResult.Ok(api.returnmessage, exchange))
            : Task.FromResult(MesResult.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
    }
    public Task<MesResult<MesReelScanResult>> ScanReelAsync(MesReelScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string barcode = string.IsNullOrWhiteSpace(request.Barcode) ? request.ReelId : request.Barcode;
        var api = new mesAPIReelQuery
        {
            txnid = mesAPI.NewTxnID(string.Empty),
            barcode = barcode,
            equipmentid = request.Context.MachineId,
            workorderno = request.WorkOrderNo,
            badgeno = request.Context.OperatorId
        };

        string raw = Send(api);
        MesExchangeRecord exchange = CreateExchange("reelquery", api.returncode, api.returnmessage, api.txnid, api.ToString(), raw);
        if (api.returncode == 0)
        {
            var result = new MesReelScanResult(
                true,
                ReadApiString(api, "reelid") ?? request.ReelId,
                api.returnmessage,
                ReadApiString(api, "matno"),
                ReadApiString(api, "tpno"));
            return Task.FromResult(MesResult<MesReelScanResult>.Ok(result, api.returnmessage, exchange));
        }

        return Task.FromResult(MesResult<MesReelScanResult>.Fail(api.returncode.ToString(), api.returnmessage, exchange: exchange));
    }

    private static string? ReadApiString(object api, string propertyName)
        => api.GetType().GetProperty(propertyName)?.GetValue(api)?.ToString();

    private string Send(mesAPI api)
    {
        CyntecMesApiLogScope? logScope = WriteApiStartLog(api);
        try
        {
            return socket.SendByAPI(api);
        }
        finally
        {
            WriteApiEndLog(api, logScope);
        }
    }

    private CyntecMesApiLogScope? WriteApiStartLog(mesAPI api)
    {
        try
        {
            return apiLogger.WriteStart(api);
        }
        catch
        {
            // MES logging must not change MES communication behavior.
            return null;
        }
    }

    private void WriteApiEndLog(mesAPI api, CyntecMesApiLogScope? logScope)
    {
        try
        {
            apiLogger.WriteEnd(api, logScope);
        }
        catch
        {
            // MES logging must not change MES communication behavior.
        }
    }
    private void SetState(MesConnectionState value, string? message = null, Exception? exception = null)
    {
        if (state == value && message == null && exception == null)
        {
            return;
        }

        state = value;
        StateChanged?.Invoke(this, new MesStateChangedEventArgs(value, message, exception));
    }

    private static MesExchangeRecord CreateExchange(
        string operation,
        int returnCode,
        string? returnMessage,
        string? transactionId,
        string? rawRequest,
        string? rawResponse,
        MesExternalDataSource? dataSource = null)
        => new(operation, returnCode, returnMessage, transactionId, rawRequest, rawResponse, dataSource);

    private static MesExternalDataSource CreateFileSource(string filePath, string format)
    {
        DateTimeOffset? lastWriteTime = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : null;
        return new MesExternalDataSource(MesExternalDataSourceKind.File, filePath, format, lastWriteTime);
    }

    private static string BuildMesFilePath(string directory, string fileNameWithoutExtension, string extension)
    {
        string normalizedExtension = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(directory, fileNameWithoutExtension + normalizedExtension);
    }

    private static string GetExtra(MesRequestContext context, string key)
        => context.Extra != null && context.Extra.TryGetValue(key, out string? value) ? value : string.Empty;
}
