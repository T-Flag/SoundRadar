namespace SoundRadar.Analysis;

public class SpectrumAnalyzer
{
    private readonly int _fftSize;
    private readonly double[] _window;
    private readonly float[] _accumBuffer;
    private int _accumCount;

    public int FftSize => _fftSize;

    public SpectrumAnalyzer(int fftSize = 1024)
    {
        _fftSize = fftSize;
        _window = CreateHanningWindow(fftSize);
        _accumBuffer = new float[fftSize * 2]; // interleaved stereo
    }

    /// <summary>
    /// Accumulates interleaved stereo samples. Returns analysis results when enough
    /// frames have been collected (>= FftSize), or null if more data is needed.
    /// </summary>
    public (double[] Left, double[] Right)? AccumulateAndAnalyze(float[] interleavedSamples, int sampleRate)
    {
        int incomingFrames = interleavedSamples.Length / 2;
        int spaceLeft = _fftSize - _accumCount;

        int toCopy = Math.Min(incomingFrames, spaceLeft);
        Array.Copy(interleavedSamples, 0, _accumBuffer, _accumCount * 2, toCopy * 2);
        _accumCount += toCopy;

        if (_accumCount < _fftSize)
            return null;

        // We have enough — analyze and reset
        var result = Analyze(_accumBuffer, sampleRate);
        _accumCount = 0;

        // If there are leftover samples, start accumulating them
        int leftover = incomingFrames - toCopy;
        if (leftover > 0)
        {
            int leftoverToCopy = Math.Min(leftover, _fftSize);
            Array.Copy(interleavedSamples, toCopy * 2, _accumBuffer, 0, leftoverToCopy * 2);
            _accumCount = leftoverToCopy;
        }

        return result;
    }

    /// <summary>
    /// Analyzes interleaved stereo buffer [L,R,L,R,...] and returns magnitude spectra for each channel.
    /// Buffer must contain at least FftSize frames.
    /// </summary>
    public (double[] Left, double[] Right) Analyze(float[] interleavedSamples, int sampleRate)
    {
        int frameCount = interleavedSamples.Length / 2;
        int frames = Math.Min(frameCount, _fftSize);

        var leftSamples = new double[_fftSize];
        var rightSamples = new double[_fftSize];

        for (int i = 0; i < frames; i++)
        {
            leftSamples[i] = interleavedSamples[i * 2] * _window[i];
            rightSamples[i] = interleavedSamples[i * 2 + 1] * _window[i];
        }

        var leftFft = FftSharp.FFT.ForwardReal(leftSamples);
        var rightFft = FftSharp.FFT.ForwardReal(rightSamples);

        var leftMag = new double[leftFft.Length];
        var rightMag = new double[rightFft.Length];
        for (int i = 0; i < leftFft.Length; i++)
            leftMag[i] = leftFft[i].Magnitude;
        for (int i = 0; i < rightFft.Length; i++)
            rightMag[i] = rightFft[i].Magnitude;

        return (leftMag, rightMag);
    }

    private static double[] CreateHanningWindow(int size)
    {
        var window = new double[size];
        for (int i = 0; i < size; i++)
            window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
        return window;
    }
}
