namespace Wirtualne_okno;

public partial class App : Application
{
    public App(IHeadTrackingService headTrackingService)
    {
        InitializeComponent();
        MainPage = new MainPage(headTrackingService);
    }
}