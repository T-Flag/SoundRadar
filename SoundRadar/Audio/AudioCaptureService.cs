using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SoundRadar.Audio;

public class AudioCaptureService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private bool _disposed;

    public event Action<float[], int>? AudioDataAvailable;
    public int ChannelCount { get; private set; } = 2;

    public void Start()
    {
        _capture = new WasapiLoopbackCapture();
        ChannelCount = _capture.WaveFormat.Channels;
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

        // Expose raw multichannel buffer — ChannelCount tells consumers the layout
        // For stereo (2ch): interleaved [L, R, L, R, ...]
        // For 7.1 (8ch): interleaved [FL, FR, FC, LFE, RL, RR, SL, SR, ...]
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
