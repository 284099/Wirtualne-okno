using System.Numerics;

#if ANDROID
using Android.Content;
using Android.Runtime;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Java.Util.Concurrent;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Face;
#endif

namespace Wirtualne_okno.Platforms.Android;

#if ANDROID
public class AndroidHeadTrackingService : Java.Lang.Object, IHeadTrackingService, ImageAnalysis.IAnalyzer
#else
public class AndroidHeadTrackingService : IHeadTrackingService
#endif
{
    private Vector3 _rawPosition = new(0f, 0f, 2.5f);
    private Vector3 _smoothedPosition = new(0f, 0f, 2.5f);

    public Vector3 CurrentSmoothedPosition => _smoothedPosition;
    public Vector3 RawPosition => _rawPosition;
    public float EyePixelDistance { get; private set; } = 0f;
    public bool IsFaceDetected { get; private set; } = false;

    private const float ReferenceEyePixelsAtBaseDistance = 70.0f;
    private const float BaseDistanceZ = 2.5f;

#if ANDROID
    private IFaceDetector? _faceDetector;
    private IExecutorService? _cameraExecutor;
    private ProcessCameraProvider? _cameraProvider;
#endif

    public async Task StartAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
            return;

#if ANDROID
        InitializeCameraAndAi();
#endif
    }

#if ANDROID
    private void InitializeCameraAndAi()
    {
        var options = new FaceDetectorOptions.Builder()
            .SetPerformanceMode(FaceDetectorOptions.PerformanceModeFast)
            .SetLandmarkMode(FaceDetectorOptions.LandmarkModeAll)
            .SetClassificationMode(FaceDetectorOptions.ClassificationModeNone)
            .SetMinFaceSize(0.15f)
            .Build();

        _faceDetector = FaceDetection.GetClient(options);
        _cameraExecutor = Executors.NewSingleThreadExecutor();

        var context = Platform.AppContext;
        var future = ProcessCameraProvider.GetInstance(context);

        future.AddListener(new Java.Lang.Runnable(() =>
        {
            try
            {
                _cameraProvider = (ProcessCameraProvider)future.Get();

                var analysis = new ImageAnalysis.Builder()
                    .SetTargetResolution(new global::Android.Util.Size(320, 240))
                    .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                    .Build();

                analysis.SetAnalyzer(_cameraExecutor, this);
                var selector = CameraSelector.DefaultFrontCamera;

                _cameraProvider.UnbindAll();
                if (Platform.CurrentActivity is ILifecycleOwner owner)
                {
                    _cameraProvider.BindToLifecycle(owner, selector, analysis);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Camera init error: {ex.Message}");
            }
        }), ContextCompat.GetMainExecutor(context));
    }

    public void Analyze(IImageProxy imageProxy)
    {
        var mediaImage = imageProxy.Image;
        if (mediaImage == null || _faceDetector == null)
        {
            imageProxy.Close();
            return;
        }

        int rotation = imageProxy.ImageInfo.RotationDegrees;
        var inputImage = InputImage.FromMediaImage(mediaImage, rotation);

        bool isRotated = rotation == 90 || rotation == 270;
        float frameWidth = isRotated ? imageProxy.Height : imageProxy.Width;
        float frameHeight = isRotated ? imageProxy.Width : imageProxy.Height;

        _faceDetector.Process(inputImage)
            .AddOnSuccessListener(new FaceSuccessListener(faces =>
            {
                if (faces == null || faces.Count == 0)
                {
                    IsFaceDetected = false;
                    return;
                }

                var face = faces[0];
                var leftEye = face.GetLandmark(FaceLandmark.LeftEye);
                var rightEye = face.GetLandmark(FaceLandmark.RightEye);

                if (leftEye != null && rightEye != null)
                {
                    IsFaceDetected = true;
                    float lx = leftEye.Position.X;
                    float ly = leftEye.Position.Y;
                    float rx = rightEye.Position.X;
                    float ry = rightEye.Position.Y;

                    // 1. Środek między źrenicami względem środka kadru
                    float midX = (lx + rx) * 0.5f;
                    float midY = (ly + ry) * 0.5f;

                    // 2. Programowe odwrócenie osi X (kamera lustrzana)
                    float normX = -((midX - frameWidth * 0.5f) / (frameWidth * 0.5f));
                    float normY = -((midY - frameHeight * 0.5f) / (frameHeight * 0.5f));

                    // 3. Obliczenie odległości między oczami (piksele) -> oś Z
                    float dx = rx - lx;
                    float dy = ry - ly;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    EyePixelDistance = dist;

                    float z = BaseDistanceZ * (ReferenceEyePixelsAtBaseDistance / Math.Max(dist, 15f));
                    _rawPosition = new Vector3(normX, normY, Math.Clamp(z, 0.5f, 5.0f));
                }
                else
                {
                    IsFaceDetected = false;
                }
            }))
            .AddOnCompleteListener(new FaceCompleteListener(() =>
            {
                imageProxy.Close();
            }));
    }

    private class FaceSuccessListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnSuccessListener
    {
        private readonly Action<IList<Face>?> _action;
        public FaceSuccessListener(Action<IList<Face>?> action) => _action = action;
        public void OnSuccess(Java.Lang.Object? result)
        {
            if (result != null)
            {
                var list = result.JavaCast<JavaList<Face>>();
                _action(list);
            }
            else
            {
                _action(null);
            }
        }
    }

    private class FaceCompleteListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnCompleteListener
    {
        private readonly Action _action;
        public FaceCompleteListener(Action action) => _action = action;
        public void OnComplete(global::Android.Gms.Tasks.Task task) => _action();
    }
#endif

    public void UpdateSmoothing()
    {
        _smoothedPosition = Vector3.Lerp(_smoothedPosition, _rawPosition, 0.20f);
    }

    public void Stop()
    {
#if ANDROID
        _cameraProvider?.UnbindAll();
        _cameraExecutor?.Shutdown();
#endif
    }
}