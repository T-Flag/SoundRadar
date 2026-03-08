using System.Windows;
using SoundRadar.Analysis;
using SoundRadar.Audio;
using SoundRadar.Overlay;

namespace SoundRadar;

public partial class App : Application
{
    private AudioCaptureService? _audioCaptureService;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var overlay = new OverlayWindow();
        var analyzer = new DirectionAnalyzer();
        _audioCaptureService = new AudioCaptureService();

        overlay.SetAnalyzer(analyzer);

        analyzer.SoundDetected += evt =>
        {
            overlay.Dispatcher.Invoke(() => overlay.PushEvent(evt));
        };

        _audioCaptureService.AudioDataAvailable += (samples, sampleRate) =>
        {
            analyzer.ProcessBuffer(samples, sampleRate);
        };

        overlay.Show();
        _audioCaptureService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _audioCaptureService?.Dispose();
        base.OnExit(e);
    }
}
