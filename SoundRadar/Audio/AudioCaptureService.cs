using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SoundRadar.Audio;

public class AudioCaptureService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private bool _disposed;

    public event Action<float[], int>? AudioDataAvailable;

    public void Start()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture?.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var waveFormat = _capture!.WaveFormat;
        int bytesPerSample = waveFormat.BitsPerSample / 8;
        int sampleCount = e.BytesRecorded / bytesPerSample;
        if (sampleCount == 0) return;

        var samples = new float[sampleCount];

        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
        }
        else
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                samples[i] = sample / 32768f;
            }
        }

        // If more than 2 channels, downmix to stereo
        if (waveFormat.Channels > 2)
        {
            int channels = waveFormat.Channels;
            int frames = sampleCount / channels;
            var stereo = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                stereo[i * 2] = samples[i * channels];         // Left
                stereo[i * 2 + 1] = samples[i * channels + 1]; // Right
            }
            samples = stereo;
        }

        AudioDataAvailable?.Invoke(samples, waveFormat.SampleRate);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _capture?.StopRecording();
        _capture?.Dispose();
    }
}
