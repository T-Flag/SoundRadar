using System.Windows;
using SoundRadar.Analysis;
using SoundRadar.Audio;
using SoundRadar.Models;
using SoundRadar.Overlay;

namespace SoundRadar;

public partial class App : Application
{
    private AudioCaptureService? _audioCaptureService;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var config = AppConfig.Load();

        var analyzer = new DirectionAnalyzer(config.IntensityThreshold, config.MaxExpectedPan);
        var spectrumAnalyzer = new SpectrumAnalyzer(1024);
        var bandFilter = new FrequencyBandFilter(
            subBass: (config.FrequencyBands.SubBass[0], config.FrequencyBands.SubBass[1]),
            lowMid: (config.FrequencyBands.LowMid[0], config.FrequencyBands.LowMid[1]),
            mid: (config.FrequencyBands.Mid[0], config.FrequencyBands.Mid[1]),
            highMid: (config.FrequencyBands.HighMid[0], config.FrequencyBands.HighMid[1])
        );
        var adaptiveThreshold = new AdaptiveThreshold(
            config.AdaptiveThreshold.AdaptationTimeSec,
            config.AdaptiveThreshold.TriggerFactor
        );

        var overlay = new OverlayWindow();
        _audioCaptureService = new AudioCaptureService();

        overlay.SetAnalyzer(analyzer);
        overlay.SetConfig(config);

        if (!config.OverlayVisible)
            overlay.SetOverlayVisible(false);

        // Legacy DirectionAnalyzer pipeline (fallback)
        analyzer.SoundDetected += evt =>
        {
            overlay.Dispatcher.Invoke(() => overlay.PushEvent(evt));
        };

        _audioCaptureService.AudioDataAvailable += (samples, sampleRate) =>
        {
            // Legacy direction analysis
            analyzer.ProcessBuffer(samples, sampleRate);

            // New FFT pipeline
            if (samples.Length / 2 >= spectrumAnalyzer.FftSize)
            {
                var (leftMag, rightMag) = spectrumAnalyzer.Analyze(samples, sampleRate);
                var bands = bandFilter.Analyze(leftMag, rightMag, sampleRate, spectrumAnalyzer.FftSize);

                // Update spectrum display
                overlay.Dispatcher.Invoke(() => overlay.UpdateSpectrum(bands));

                // Adaptive threshold filtering
                double frameDuration = (double)spectrumAnalyzer.FftSize / sampleRate;
                var triggered = adaptiveThreshold.Process(bands, frameDuration);

                // Emit events for top 3 triggered bands
                var top3 = triggered
                    .OrderByDescending(b => b.Energy)
                    .Take(3);

                foreach (var band in top3)
                {
                    float normalizedPan = DirectionAnalyzer.NormalizePan(band.Pan, analyzer.MaxExpectedPan);
                    var evt = new SoundEvent
                    {
                        Pan = normalizedPan,
                        Intensity = band.Intensity,
                        DominantFrequency = 0f,
                        DominantBand = band.Name,
                    };
                    overlay.Dispatcher.Invoke(() => overlay.PushEvent(evt));
                }
            }
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
