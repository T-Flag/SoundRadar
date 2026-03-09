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
        // Force software rendering — overlay is lightweight, avoids GPU starvation by games
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        var config = AppConfig.Load();

        var analyzer = new DirectionAnalyzer(config.MaxExpectedPan);
        var spectrumAnalyzer = new SpectrumAnalyzer(1024);
        var bandFilter = new FrequencyBandFilter(
            subBass: (config.FrequencyBands.SubBass[0], config.FrequencyBands.SubBass[1]),
            lowMid: (config.FrequencyBands.LowMid[0], config.FrequencyBands.LowMid[1]),
            mid: (config.FrequencyBands.Mid[0], config.FrequencyBands.Mid[1]),
            highMid: (config.FrequencyBands.HighMid[0], config.FrequencyBands.HighMid[1]),
            noiseFloorDb: config.FrequencyBands.NoiseFloorDb,
            ceilingDb: config.FrequencyBands.CeilingDb
        );
        var adaptiveThreshold = new AdaptiveThreshold(
            config.AdaptiveThreshold.AdaptationTimeSec,
            config.AdaptiveThreshold.TriggerFactor
        );

        var overlay = new OverlayWindow();
        _audioCaptureService = new AudioCaptureService();

        overlay.SetAnalyzer(analyzer);
        overlay.SetConfig(config);
        overlay.SetAdaptiveThreshold(adaptiveThreshold);
        overlay.SetBandFilter(bandFilter);

        if (!config.OverlayVisible)
            overlay.SetOverlayVisible(false);

        // Create surround analyzer with configurable angles
        SurroundAnalyzer? surroundAnalyzer = null;
        bool isSurround = false;

        // Legacy DirectionAnalyzer pipeline (fallback)
        analyzer.SoundDetected += evt =>
        {
            overlay.Dispatcher.Invoke(() => overlay.PushEvent(evt));
        };

        _audioCaptureService.AudioDataAvailable += (samples, sampleRate) =>
        {
            // Detect surround on first buffer
            if (surroundAnalyzer == null && _audioCaptureService.ChannelCount >= 8 && config.Surround.Enabled)
            {
                surroundAnalyzer = new SurroundAnalyzer(config.Surround.ChannelAngles);
                isSurround = true;
                overlay.Dispatcher.Invoke(() => overlay.SetSurroundMode(true));
            }

            // Downmix to stereo for legacy analyzer + FFT pipeline
            float[] stereoSamples;
            if (_audioCaptureService.ChannelCount > 2)
                stereoSamples = SurroundAnalyzer.DownmixToStereo(samples, _audioCaptureService.ChannelCount);
            else
                stereoSamples = samples;

            // Surround analysis (7.1 angle)
            float surroundAngle = 0f;
            float surroundIntensity = 0f;
            if (isSurround && surroundAnalyzer != null)
            {
                var surroundResult = surroundAnalyzer.Analyze(samples, _audioCaptureService.ChannelCount);
                if (surroundResult != null)
                {
                    surroundAngle = surroundResult.Value.Angle;
                    surroundIntensity = surroundResult.Value.Intensity;
                }
            }

            // Legacy direction analysis (stereo)
            analyzer.ProcessBuffer(stereoSamples, sampleRate);

            // FFT pipeline with sample accumulation
            var fftResult = spectrumAnalyzer.AccumulateAndAnalyze(stereoSamples, sampleRate);

            if (fftResult.HasValue)
            {
                var (leftMag, rightMag) = fftResult.Value;
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
                    SoundEvent evt;
                    if (isSurround)
                    {
                        evt = new SoundEvent
                        {
                            Pan = 0f,
                            Angle = surroundAngle,
                            IsSurround = true,
                            Intensity = band.Intensity,
                            DominantFrequency = 0f,
                            DominantBand = band.Name,
                        };
                    }
                    else
                    {
                        float normalizedPan = DirectionAnalyzer.NormalizePan(band.Pan, analyzer.MaxExpectedPan);
                        evt = new SoundEvent
                        {
                            Pan = normalizedPan,
                            Intensity = band.Intensity,
                            DominantFrequency = 0f,
                            DominantBand = band.Name,
                        };
                    }
                    overlay.Dispatcher.Invoke(() => overlay.PushEvent(evt));
                }
            }

            // Update debug data (after FFT pipeline so baseline is current)
            var debugData = new DebugData
            {
                SampleRate = sampleRate,
                BufferSize = stereoSamples.Length / 2,
                CaptureActive = true,
                RawPan = analyzer.LastRawPan,
                NormalizedPan = analyzer.LastNormalizedPan,
                RawIntensity = analyzer.LastRawIntensity,
                MaxExpectedPan = analyzer.MaxExpectedPan,
                BaselineAvg = adaptiveThreshold.GetAverage("Mid"),
                TriggerLevel = adaptiveThreshold.GetAverage("Mid") * adaptiveThreshold.TriggerFactor,
                TriggerFactor = adaptiveThreshold.TriggerFactor,
                IsSurround = isSurround,
                ChannelCount = _audioCaptureService.ChannelCount,
                SurroundAngle = surroundAngle,
                SurroundIntensity = surroundIntensity,
                ChannelEnergies = surroundAnalyzer?.LastChannelEnergies ?? Array.Empty<float>(),
            };
            overlay.Dispatcher.Invoke(() => overlay.UpdateDebugData(debugData));
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
