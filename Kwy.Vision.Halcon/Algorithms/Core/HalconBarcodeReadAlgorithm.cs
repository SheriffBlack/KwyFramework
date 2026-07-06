using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconBarcodeReadAlgorithm
    : HalconVisionAlgorithm<BarcodeReadRequest, BarcodeReadResult>
{
    public const string Id = "BarcodeRead";

    private readonly HalconVisionImageConverter converter;

    public HalconBarcodeReadAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<BarcodeReadResult>> ExecuteAsync(
        BarcodeReadRequest request,
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
            using var model = new HBarCode();
            model.CreateBarCodeModel(new HTuple(), new HTuple());
            ApplyParameters(model, request);
            using HRegion foundRegion = model.FindBarCode(
                workingImage,
                ToCodeTypes(request.CodeTypes),
                out HTuple decodedData);

            var codes = new List<VisionCodeRead>(Math.Min(decodedData.Length, request.MaximumCount));
            var overlays = request.EnableOverlay
                ? new List<IVisionOverlayShape>(Math.Min(decodedData.Length, request.MaximumCount))
                : null;
            for (int i = 0; i < decodedData.Length && i < request.MaximumCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VisionContour? contour = TryCreateRegionContour(foundRegion, i + 1);
                codes.Add(new VisionCodeRead(decodedData[i].S, request.CodeTypes?.FirstOrDefault() ?? "auto", contour));
                if (contour != null && overlays != null)
                {
                    overlays.Add(new OverlayContour(contour, VisionColor.Cyan, 1.5, $"Barcode {i + 1}"));
                }
            }

            stopwatch.Stop();
            return VisionExecutionResult<BarcodeReadResult>.Success(
                new BarcodeReadResult(codes),
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
            return VisionExecutionResult<BarcodeReadResult>.Failure(
                "HALCON_BARCODE_READ_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static HTuple ToCodeTypes(IReadOnlyList<string>? codeTypes)
        => codeTypes is { Count: > 0 }
            ? new HTuple(codeTypes.ToArray())
            : new HTuple("auto");

    private static void ApplyParameters(HBarCode model, BarcodeReadRequest request)
    {
        if (request.TimeoutMilliseconds.HasValue)
        {
            model.SetBarCodeParam("timeout", request.TimeoutMilliseconds.Value);
        }

        if (request.MinimumContrast.HasValue)
        {
            model.SetBarCodeParam("contrast_min", request.MinimumContrast.Value);
        }

        if (request.Polarity != CodePolarity.Any)
        {
            model.SetBarCodeParam("polarity", ToHalconPolarity(request.Polarity));
        }
    }

    private static string ToHalconPolarity(CodePolarity polarity) => polarity switch
    {
        CodePolarity.Any => "any",
        CodePolarity.DarkOnLight => "dark_on_light",
        CodePolarity.LightOnDark => "light_on_dark",
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, null)
    };

    private static VisionContour? TryCreateRegionContour(HRegion regions, int index)
    {
        if (index < 1 || index > regions.CountObj())
        {
            return null;
        }

        using HRegion region = regions.SelectObj(index);
        region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
        if (row1.Length == 0 || column1.Length == 0 || row2.Length == 0 || column2.Length == 0)
        {
            return null;
        }

        double left = column1[0].D;
        double top = row1[0].D;
        double right = column2[0].D;
        double bottom = row2[0].D;
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom))
        {
            return null;
        }

        return new VisionContour(
            new[]
            {
                new VisionPoint(left, top),
                new VisionPoint(right, top),
                new VisionPoint(right, bottom),
                new VisionPoint(left, bottom)
            },
            isClosed: true);
    }

    private static void Validate(BarcodeReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        if (request.CodeTypes != null && request.CodeTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Barcode code types cannot contain empty values.", nameof(request));
        }

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
}
