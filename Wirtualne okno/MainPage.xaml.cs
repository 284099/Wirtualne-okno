using System.Numerics;

namespace Wirtualne_okno;

public partial class MainPage : ContentPage
{
    private readonly IHeadTrackingService _headTrackingService;
    private IDispatcherTimer? _displayTimer;

    public MainPage(IHeadTrackingService headTrackingService)
    {
        InitializeComponent();
        _headTrackingService = headTrackingService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _headTrackingService.StartAsync();

        _displayTimer = Dispatcher.CreateTimer();
        _displayTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30 FPS
        _displayTimer.Tick += OnUpdateTelemetry;
        _displayTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _displayTimer?.Stop();
        _headTrackingService.Stop();
    }

    private void OnUpdateTelemetry(object? sender, EventArgs e)
    {
        _headTrackingService.UpdateSmoothing();

        Vector3 smoothed = _headTrackingService.CurrentSmoothedPosition;
        Vector3 raw = _headTrackingService.RawPosition;
        bool faceDetected = _headTrackingService.IsFaceDetected;

        if (faceDetected)
        {
            AiStatusLabel.Text = "Twarz wykryta (ML Kit)";
            AiStatusLabel.TextColor = Color.FromRgb(63, 185, 80);
        }
        else
        {
            AiStatusLabel.Text = "Szukanie twarzy przed kamerą...";
            AiStatusLabel.TextColor = Color.FromRgb(255, 166, 87);
        }

        PixelDistanceLabel.Text = $"Rozstaw źrenic na klatce: {_headTrackingService.EyePixelDistance:F1} px";

        PosXLabel.Text = $"{smoothed.X:F2}";
        PosYLabel.Text = $"{smoothed.Y:F2}";
        PosZLabel.Text = $"{smoothed.Z:F2}";

        RawCoordsLabel.Text = $"Przed filtrem: X={raw.X:F2}, Y={raw.Y:F2}, Z={raw.Z:F2}";

        // Przesunięcie kropki w celowniku
        HeadMarker.TranslationX = Math.Clamp(smoothed.X * 100f, -120f, 120f);
        HeadMarker.TranslationY = Math.Clamp(-smoothed.Y * 50f, -50f, 50f);
    }
}