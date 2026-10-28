using System.Numerics;

namespace Wirtualne_okno;

public interface IHeadTrackingService
{
    Vector3 CurrentSmoothedPosition { get; }
    Vector3 RawPosition { get; }
    float EyePixelDistance { get; }
    bool IsFaceDetected { get; }

    Task StartAsync();
    void Stop();
    void UpdateSmoothing();
}