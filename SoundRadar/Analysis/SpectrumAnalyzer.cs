namespace SoundRadar.Analysis;

public class SpectrumAnalyzer
{
    private readonly int _fftSize;
    private readonly double[] _window;

    public int FftSize => _fftSize;

    public SpectrumAnalyzer(int fftSize = 1024)
    {
        _fftSize = fftSize;
        _window = CreateHanningWindow(fftSize);
    }

    /// <summary>
    /// Analyzes interleaved stereo buffer [L,R,L,R,...] and returns magnitude spectra for each channel.
    /// Returns (leftMagnitudes, rightMagnitudes) each of length FftSize/2.
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
