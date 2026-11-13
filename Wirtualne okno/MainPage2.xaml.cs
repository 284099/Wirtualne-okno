using System.Numerics;

namespace Wirtualne_okno;

public partial class MainPage2 : ContentPage
{
    private readonly IHeadTrackingService _headTrackingService;
    private IDispatcherTimer? _displayTimer;
    private bool _isWebViewLoaded = false;

    public MainPage2(IHeadTrackingService headTrackingService)
    {
        InitializeComponent();
        _headTrackingService = headTrackingService;

        SceneWebView.Navigated += (s, e) => _isWebViewLoaded = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // £adowanie lokalnej sceny Three.js z index.html
        if (SceneWebView.Source == null)
        {
            await LoadLocal3DScene();
        }

        await _headTrackingService.StartAsync();

        _displayTimer = Dispatcher.CreateTimer();
        _displayTimer.Interval = TimeSpan.FromMilliseconds(33);
        _displayTimer.Tick += OnUpdate3DScene;
        _displayTimer.Start();
    }

    private async Task LoadLocal3DScene()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("index.html");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();

        SceneWebView.Source = new HtmlWebViewSource { Html = html };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _displayTimer?.Stop();

        // Zatrzymujemy kamerê, aby zwolniæ zasoby gdy zak³adka jest nieaktywna
        _headTrackingService.Stop();
    }

    private async void OnUpdate3DScene(object? sender, EventArgs e)
    {
        _headTrackingService.UpdateSmoothing();

        Vector3 smoothed = _headTrackingService.CurrentSmoothedPosition;
        bool faceDetected = _headTrackingService.IsFaceDetected;

        if (_isWebViewLoaded && faceDetected)
        {
            string jsCommand = $"updateCameraPosition({smoothed.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                               $"{smoothed.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                               $"{smoothed.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)});";

            await SceneWebView.EvaluateJavaScriptAsync(jsCommand);
        }
    }
}