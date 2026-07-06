using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconDataCode2DReadAlgorithm
    : HalconVisionAlgorithm<DataCode2DReadRequest, DataCode2DReadResult>
{
    public const string Id = "DataCode2DRead";

    private readonly HalconVisionImageConverter converter;

    public HalconDataCode2DReadAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<DataCode2DReadResult>> ExecuteAsync(
        DataCode2DReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using HalconImageLease lease = await converter
                .AcquireAsync(request.Image, cancellationToken)
                .ConfigureAwait(false);
            using HRegion? region = HalconRegionFactory.Create(request.SearchRegion);
            using HImage workingImage = region == null
                ? lease.Image.CopyImage()
                : lease.Image.ReduceDomain(region);
            using var model = new HDataCode2D();
            model.CreateDataCode2dModel(request.SymbolType, new HTuple(), new HTuple());
            ApplyParameters(model, request);
            using HXLDCont contours = model.FindDataCode2d(
                workingImage,
                new HTuple(),
                new HTuple(),
                out HTuple resultHandles,
                out HTuple decodedData);
            _ = resultHandles;

            var codes = new List<VisionCodeRead>(Math.Min(decodedData.Length, request.MaximumCount));
            var overlays = request.EnableOverlay
                ? new List<IVisionOverlayShape>(Math.Min(decodedData.Length, request.MaximumCount))
                : null;
            for (int i = 0; i < decodedData.Length && i < request.MaximumCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VisionContour? contour = TryCreateContour(contours, i + 1);
                codes.Add(new VisionCodeRead(decodedData[i].S, request.SymbolType, contour));
                if (contour != null && overlays != null)
                {
                    overlays.Add(new OverlayContour(contour, VisionColor.Cyan, 1.5, $"DataCode {i + 1}"));
                }
            }

            stopwatch.Stop();
            return VisionExecutionResult<DataCode2DReadResult>.Success(
                new DataCode2DReadResult(codes),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["Count"] = codes.Count.ToString()
                },
                overlays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return VisionExecutionResult<DataCode2DReadResult>.Failure(
                "HALCON_DATA_CODE_2D_READ_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static void Validate(DataCode2DReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SymbolType);
        if (request.MaximumCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumCount));
        }

        if (request.TimeoutMilliseconds.HasValue && request.TimeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TimeoutMilliseconds));
        }

        if (request.MinimumContrast.HasValue
            && (!double.IsFinite(request.MinimumContrast.Value) || request.MinimumContrast.Value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(request.MinimumContrast));
        }
    }

    private static void ApplyParameters(HDataCode2D model, DataCode2DReadRequest request)
    {
        if (request.TimeoutMilliseconds.HasValue)
        {
            model.SetDataCode2dParam("timeout", request.TimeoutMilliseconds.Value);
        }

        if (request.MinimumContrast.HasValue)
        {
            model.SetDataCode2dParam("contrast_min", request.MinimumContrast.Value);
        }

        if (request.Polarity != CodePolarity.Any)
        {
            model.SetDataCode2dParam("polarity", ToHalconPolarity(request.Polarity));
        }
    }

    private static string ToHalconPolarity(CodePolarity polarity) => polarity switch
    {
        CodePolarity.Any => "any",
        CodePolarity.DarkOnLight => "dark_on_light",
        CodePolarity.LightOnDark => "light_on_dark",
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, null)
    };

    private static VisionContour? TryCreateContour(HXLDCont contours, int index)
    {
        if (index < 1 || index > contours.CountObj())
        {
            return null;
        }

        using HXLDCont contour = contours.SelectObj(index);
        contour.GetContourXld(out HTuple rows, out HTuple columns);
        if (rows.Length < 2 || columns.Length < 2)
        {
            return null;
        }

        var points = new VisionPoint[Math.Min(rows.Length, columns.Length)];
        for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            points[pointIndex] = new VisionPoint(columns[pointIndex].D, rows[pointIndex].D);
        }

        return new VisionContour(points, isClosed: points.Length >= 3);
    }
}
