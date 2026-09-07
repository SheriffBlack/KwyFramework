using Kwy.Vision.Abstractions;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Abstractions.Runtime;
using Kwy.Vision.DeepLearning.Onnx;
using Kwy.Vision.Halcon;
using Kwy.Vision.Halcon.Algorithms;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.OpenCV;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.Vision.Tests;

public sealed class VisionArchitectureTests
{
    [Fact]
    public async Task VisionImageBuffer_OwnsCopiedPixels()
    {
        byte[] source = [1, 2, 3, 4];
        await using var image = new VisionImageBuffer(source, 2, 2, 2, VisionPixelFormat.Mono8);
        source[0] = 99;

        ReadOnlyMemory<byte> pixels = await image.GetPixelMemoryAsync();
        Assert.Equal(1, pixels.Span[0]);
    }

    [Fact]
    public void VisionImageBuffer_CalculatesPackedStrideWhenMissing()
    {
        using var image = new VisionImageBuffer(
            new byte[18],
            width: 3,
            height: 2,
            stride: 0,
            VisionPixelFormat.Bgr24);

        Assert.Equal(9, image.Stride);
    }

    [Fact]
    public void GeometryCollections_CopyInputAndValidateClosedRegions()
    {
        var points = new List<VisionPoint>
        {
            new(0, 0),
            new(10, 0),
            new(10, 10)
        };

        var polygon = new PolygonRegion(points);
        var contour = new VisionContour(points, isClosed: true);
        var composite = new CompositeRegion(
            polygon,
            [new CircleRegion(new VisionCircle(new VisionPoint(5, 5), 2))]);
        points.Clear();

        Assert.Equal(3, polygon.Points.Count);
        Assert.Equal(3, contour.Points.Count);
        Assert.Single(composite.Holes);
        Assert.Throws<ArgumentException>(() =>
            new ContourRegion(new VisionContour([new(0, 0), new(1, 1)], isClosed: false)));
    }

    [Fact]
    public void ThreeDimensionalGeometry_NormalizesAndMeasuresPlaneDistance()
    {
        var plane = new VisionPlane(
            new VisionPoint3D(0, 0, 5),
            new VisionVector3D(0, 0, 2));

        Assert.Equal(3, plane.SignedDistanceTo(new VisionPoint3D(0, 0, 8)), 10);
        Assert.Equal(1, plane.Normalize().Normal.Length, 10);
        Assert.Equal(VisionQuaternion.Identity, VisionPose3D.Identity.Orientation);
    }

    [Fact]
    public void AlgorithmRegistry_RequiresBackendWhenImplementationsAreAmbiguous()
    {
        IVisionAlgorithm[] algorithms = [new HalconThreshold(), new OpenCvThreshold()];
        var registry = new VisionAlgorithmRegistry(algorithms);

        Assert.Throws<InvalidOperationException>(() =>
            registry.GetRequired<ThresholdRequest, ThresholdResult>("Threshold"));
        Assert.IsType<HalconThreshold>(
            registry.GetRequired<ThresholdRequest, ThresholdResult>("Threshold", VisionBackendIds.Halcon));
    }

    [Fact]
    public async Task VisionModel_RequiresLoadAndOwnsLifecycle()
    {
        await using var model = new FakeOnnxModel(new OnnxVisionModelConfig
        {
            ModelId = "DefectDetection",
            ModelPath = "model.onnx"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await model.PredictAsync("input"));
        await model.LoadAsync();
        Assert.Equal("INPUT", await model.PredictAsync("input"));
        Assert.Equal(VisionModelState.Loaded, model.State);
        await model.UnloadAsync();
        Assert.Equal(VisionModelState.Unloaded, model.State);
    }

    [Fact]
    public async Task HalconRotationCenterAlgorithm_FitsPointsCorrectly()
    {
        if (!IsHalconAvailable()) return;

        var services = new ServiceCollection();
        services.AddKwyHalconVision();
        using ServiceProvider provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IVisionAlgorithmRegistry>();
        var algorithm = registry.GetRequired<RotationCenterCalibrationRequest, RotationCenterCalibrationResult>(
            HalconRotationCenterAlgorithm.Id,
            VisionBackendIds.Halcon);

        // Define a circle at (100, 150) with radius 50
        // Generate points: (150, 150), (100, 200), (50, 150)
        var points = new List<VisionPoint>
        {
            new(150, 150),
            new(100, 200),
            new(50, 150)
        };

        var request = new RotationCenterCalibrationRequest(points);
        var result = await algorithm.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(100, result.Value.PixelCenter.X, 1);
        Assert.Equal(150, result.Value.PixelCenter.Y, 1);
        Assert.Equal(50, result.Value.RadiusPixels, 1);
        Assert.True(result.Value.ResidualPixels < 0.001);
    }

    [Fact]
    public async Task HalconFixtureAlgorithm_ComputesTransformCorrectly()
    {
        if (!IsHalconAvailable()) return;

        var services = new ServiceCollection();
        services.AddKwyHalconVision();
        using ServiceProvider provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IVisionAlgorithmRegistry>();
        var algorithm = registry.GetRequired<FixtureRequest, FixtureResult>(
            HalconFixtureAlgorithm.Id,
            VisionBackendIds.Halcon);

        var refPose = new VisionPose2D(100.0, 150.0, 0.0);
        // Translation X+10, Y-20, Rotation 90 degrees (Math.PI / 2)
        var curPose = new VisionPose2D(110.0, 130.0, Math.PI / 2);

        var request = new FixtureRequest(curPose, refPose);
        var result = await algorithm.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);

        var transformedPt = result.Value.Transform.Transform(new VisionPoint(100.0, 150.0));
        Assert.Equal(110.0, transformedPt.X, 2);
        Assert.Equal(130.0, transformedPt.Y, 2);
    }

    [Fact]
    public void DependencyInjection_RegistersAllBackendsAndTypedComponents()
    {
        var services = new ServiceCollection();
        services.AddKwyHalconVision();
        services.AddKwyOpenCvVision();
        services.AddKwyOnnxVision();
        services.AddVisionAlgorithm<HalconThreshold>();
        services.AddVisionModel<FakeOnnxModel>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IVisionBackendCatalog catalog = provider.GetRequiredService<IVisionBackendCatalog>();
        IVisionAlgorithmRegistry algorithms = provider.GetRequiredService<IVisionAlgorithmRegistry>();
        IVisionModelRegistry models = provider.GetRequiredService<IVisionModelRegistry>();

        Assert.Equal(3, catalog.Backends.Count);
        Assert.IsType<HalconThreshold>(
            algorithms.GetRequired<ThresholdRequest, ThresholdResult>("Threshold", VisionBackendIds.Halcon));
        Assert.IsType<HalconEdgeMeasurementAlgorithm>(
            algorithms.GetRequired<EdgeMeasurementRequest, EdgeMeasurementResult>(
                HalconEdgeMeasurementAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconPlanarCalibrationAlgorithm>(
            algorithms.GetRequired<PlanarCalibrationRequest, PlanarCalibrationResult>(
                HalconPlanarCalibrationAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconContourDetectionAlgorithm>(
            algorithms.GetRequired<ContourDetectionRequest, ContourDetectionResult>(
                HalconContourDetectionAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconRotationCenterAlgorithm>(
            algorithms.GetRequired<RotationCenterCalibrationRequest, RotationCenterCalibrationResult>(
                HalconRotationCenterAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconFixtureAlgorithm>(
            algorithms.GetRequired<FixtureRequest, FixtureResult>(
                HalconFixtureAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconLineFittingAlgorithm>(
            algorithms.GetRequired<LineFittingRequest, LineFittingResult>(
                HalconLineFittingAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconCircleFittingAlgorithm>(
            algorithms.GetRequired<CircleFittingRequest, CircleFittingResult>(
                HalconCircleFittingAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconDistanceMeasurementAlgorithm>(
            algorithms.GetRequired<DistanceMeasurementRequest, DistanceMeasurementResult>(
                HalconDistanceMeasurementAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconCaliperGroupMeasurementAlgorithm>(
            algorithms.GetRequired<CaliperGroupMeasurementRequest, CaliperGroupMeasurementResult>(
                HalconCaliperGroupMeasurementAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconBlobFeatureInspectionAlgorithm>(
            algorithms.GetRequired<BlobFeatureInspectionRequest, BlobFeatureInspectionResult>(
                HalconBlobFeatureInspectionAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconContourFittingAlgorithm>(
            algorithms.GetRequired<ContourFittingRequest, ContourFittingResult>(
                HalconContourFittingAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconImagePreprocessAlgorithm>(
            algorithms.GetRequired<ImagePreprocessRequest, ImagePreprocessResult>(
                HalconImagePreprocessAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconGeometryMeasurementAlgorithm>(
            algorithms.GetRequired<GeometryMeasurementRequest, GeometryMeasurementResult>(
                HalconGeometryMeasurementAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconBarcodeReadAlgorithm>(
            algorithms.GetRequired<BarcodeReadRequest, BarcodeReadResult>(
                HalconBarcodeReadAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconDataCode2DReadAlgorithm>(
            algorithms.GetRequired<DataCode2DReadRequest, DataCode2DReadResult>(
                HalconDataCode2DReadAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconLineMetrologyAlgorithm>(
            algorithms.GetRequired<LineMetrologyRequest, LineMetrologyResult>(
                HalconLineMetrologyAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<HalconCircleMetrologyAlgorithm>(
            algorithms.GetRequired<CircleMetrologyRequest, CircleMetrologyResult>(
                HalconCircleMetrologyAlgorithm.Id,
                VisionBackendIds.Halcon));
        Assert.IsType<FakeOnnxModel>(models.GetRequired<string, string>("DefectDetection"));
    }

    [Fact]
    public void DependencyInjection_RegistrationMethodsAreIdempotentAndConvertersAreKeyedByBackend()
    {
        var services = new ServiceCollection();
        services.AddKwyHalconVision();
        services.AddKwyHalconVision();
        services.AddKwyOpenCvVision();
        services.AddKwyOpenCvVision();
        services.AddKwyOnnxVision();
        services.AddKwyOnnxVision();
        services.AddVisionAlgorithm<HalconThreshold>();
        services.AddVisionAlgorithm<HalconThreshold>();
        services.AddVisionModel<FakeOnnxModel>();
        services.AddVisionModel<FakeOnnxModel>();
        services.AddVisionImageConverter<FakeImageConverter>();
        services.AddVisionImageConverter<FakeImageConverter>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IVisionBackendCatalog catalog = provider.GetRequiredService<IVisionBackendCatalog>();
        IVisionAlgorithmRegistry algorithms = provider.GetRequiredService<IVisionAlgorithmRegistry>();
        IVisionModelRegistry models = provider.GetRequiredService<IVisionModelRegistry>();
        IVisionImageConverterRegistry converters = provider.GetRequiredService<IVisionImageConverterRegistry>();

        Assert.Equal(3, catalog.Backends.Count);
        Assert.Single(algorithms.Algorithms.OfType<HalconThreshold>());
        Assert.Single(models.Models.OfType<FakeOnnxModel>());
        Assert.IsType<HalconVisionImageConverter>(converters.GetRequired(VisionBackendIds.Halcon));
        Assert.IsType<FakeImageConverter>(converters.GetRequired(FakeImageConverter.Backend));
        Assert.Equal(2, converters.Converters.Count);
    }

    public sealed record ThresholdRequest(IVisionImage Image, byte Minimum);

    public sealed record ThresholdResult(int ForegroundPixelCount);

    public sealed class HalconThreshold : HalconVisionAlgorithm<ThresholdRequest, ThresholdResult>
    {
        public HalconThreshold() : base("Threshold")
        {
        }

        public override ValueTask<VisionExecutionResult<ThresholdResult>> ExecuteAsync(
            ThresholdRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(VisionExecutionResult<ThresholdResult>.Success(new(0), TimeSpan.Zero));
    }

    private sealed class OpenCvThreshold : OpenCvVisionAlgorithm<ThresholdRequest, ThresholdResult>
    {
        public OpenCvThreshold() : base("Threshold")
        {
        }

        public override ValueTask<VisionExecutionResult<ThresholdResult>> ExecuteAsync(
            ThresholdRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(VisionExecutionResult<ThresholdResult>.Success(new(0), TimeSpan.Zero));
    }

    public sealed class FakeOnnxModel : OnnxVisionModel<string, string>
    {
        public FakeOnnxModel()
            : this(new OnnxVisionModelConfig
            {
                ModelId = "DefectDetection",
                ModelPath = "model.onnx"
            })
        {
        }

        public FakeOnnxModel(OnnxVisionModelConfig config) : base(config)
        {
        }

        protected override ValueTask LoadCoreAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask UnloadCoreAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask<string> PredictCoreAsync(string input, CancellationToken cancellationToken)
            => ValueTask.FromResult(input.ToUpperInvariant());
    }

    public sealed class FakeImageConverter : IVisionImageConverter
    {
        public const string Backend = "Fake";

        public string BackendId => Backend;

        public ValueTask<IVisionImage> ConvertAsync(
            IVisionImage source,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(source);
    }

    private static bool IsHalconAvailable()
    {
        try
        {
            var type = Type.GetType("HalconDotNet.HSystem, halcondotnet");
            if (type == null) return false;
            
            // Invoke GetSystem to trigger native P/Invoke loading
            var getSystemMethod = type.GetMethod("GetSystem", new[] { typeof(string) });
            if (getSystemMethod == null) return false;
            
            getSystemMethod.Invoke(null, new object[] { "version" });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
