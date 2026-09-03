using NAudio.Wave;

namespace UnikPlayer;

/// <summary>
/// Captures system output audio (WASAPI loopback) and estimates the BPM of the
/// currently playing track. Detection runs exactly once per track, on the first
/// ~4 seconds of audio, then freezes until Reset() is called (i.e. a new track).
/// Gracefully disables itself when no loopback device is available.
/// </summary>
public class BpmMonitor : IDisposable
{
    private readonly object _lock = new();
    private readonly List<float> _mono = new();
    private readonly WasapiLoopbackCapture? _capture;
    private readonly System.Threading.Timer? _detectTimer;
    private readonly int _sampleRate;
    private double? _currentBpm;
    private bool _detected; // BPM frozen for the current track until Reset

    private const double WINDOW_SECONDS = 4.0;      // analyze only the first 4s of the track
    private const double MIN_WINDOW_SECONDS = 4.0;

    public event Action<double?>? BpmChanged;

    public double? CurrentBpm
    {
        get { lock (_lock) return _currentBpm; }
    }

    public bool Available => _capture != null;

    public BpmMonitor()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
            _capture.DataAvailable += OnData;
            _capture.StartRecording();
            _detectTimer = new System.Threading.Timer(_ => Detect(), null, 3500, 1000);
            Console.WriteLine("[BPM] Loopback capture started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BPM] Loopback capture unavailable: {ex.Message}");
            _capture = null;
            _sampleRate = 0;
        }
    }

    /// <summary>
    /// Called when the playing track changes: drops old audio, clears the frozen
    /// BPM and re-arms a fresh one-shot detection over the next 4 seconds.
    /// </summary>
    public void Reset()
    {
        if (_capture == null) return;
        lock (_lock)
        {
            _mono.Clear();
            _detected = false;
            _currentBpm = null;
        }
        try { _capture.StartRecording(); } catch { }
        BpmChanged?.Invoke(null);
        Console.WriteLine("[BPM] Reset for new track");
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (_capture == null) return;
        var wf = _capture.WaveFormat;
        int channels = Math.Max(1, wf.Channels);
        int bytesPerSample = wf.BitsPerSample / 8;
        bool isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat;
        int frames = bytesPerSample > 0 ? e.BytesRecorded / (bytesPerSample * channels) : 0;

        lock (_lock)
        {
            if (_detected) return; // no need to keep buffering once BPM is frozen

            for (int f = 0; f < frames; f++)
            {
                double acc = 0;
                for (int c = 0; c < channels; c++)
                {
                    int off = (f * channels + c) * bytesPerSample;
                    if (off + bytesPerSample > e.Buffer.Length) continue;
                    double s;
                    if (isFloat && bytesPerSample == 4)
                        s = BitConverter.ToSingle(e.Buffer, off);
                    else if (!isFloat && bytesPerSample == 2)
                        s = BitConverter.ToInt16(e.Buffer, off) / 32768.0;
                    else if (!isFloat && bytesPerSample == 3)
                    {
                        int v = e.Buffer[off] | (e.Buffer[off + 1] << 8) | (e.Buffer[off + 2] << 16);
                        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                        s = v / 8388608.0;
                    }
                    else if (!isFloat && bytesPerSample == 4)
                        s = BitConverter.ToInt32(e.Buffer, off) / 2147483648.0;
                    else s = 0;
                    acc += s;
                }
                _mono.Add((float)(acc / channels));
            }

            int maxSamples = (int)(WINDOW_SECONDS * _sampleRate);
            if (maxSamples > 0 && _mono.Count > maxSamples)
                _mono.RemoveRange(0, _mono.Count - maxSamples);
        }
    }

    private void Detect()
    {
        if (_capture == null || _sampleRate <= 0) return;

        float[] buf;
        lock (_lock)
        {
            if (_detected) return; // already frozen for this track
            if (_mono.Count < _sampleRate * MIN_WINDOW_SECONDS) return; // need the full 4s window
            buf = _mono.ToArray();
        }

        var bpm = DetectBpm(buf, _sampleRate);
        lock (_lock)
        {
            _detected = true; // one attempt per track — freeze regardless of result
            _currentBpm = bpm;
        }
        try { _capture?.StopRecording(); } catch { }

        Console.WriteLine(bpm == null
            ? "[BPM] No clear tempo in first 4s"
            : $"[BPM] Detected: {bpm.Value} BPM (first 4s)");
        BpmChanged?.Invoke(bpm);
    }

    /// <summary>
    /// Tempo estimation over a short window: energy onset envelope +
    /// autocorrelation over lags covering 60-200 BPM. Returns rounded BPM or
    /// null when the signal is silent / has no clear beat.
    /// </summary>
    private static double? DetectBpm(float[] x, int sampleRate)
    {
        double maxAmp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double a = Math.Abs(x[i]);
            if (a > maxAmp) maxAmp = a;
        }
        if (maxAmp < 0.01) return null;

        int window = (int)(WINDOW_SECONDS * sampleRate);
        int start = Math.Max(0, x.Length - window);
        if (x.Length - start < sampleRate * MIN_WINDOW_SECONDS) return null;

        const int FRAME = 1024;
        const int HOP = 512;

        var envelope = new float[(x.Length - start) / HOP + 1];
        int ei = 0;
        for (int i = start; i + FRAME <= x.Length; i += HOP)
        {
            double e = 0;
            for (int j = 0; j < FRAME; j++) e += x[i + j] * x[i + j];
            envelope[ei++] = (float)Math.Sqrt(e / FRAME);
        }
        int envLen = ei;
        if (envLen < 2) return null;

        // Onset envelope: positive energy difference between consecutive frames
        var flux = new float[envLen];
        for (int i = 1; i < envLen; i++)
            flux[i] = Math.Max(0f, envelope[i] - envelope[i - 1]);

        // Remove DC (mean) so autocorrelation measures fluctuation, not level
        double mean = 0;
        for (int i = 0; i < envLen; i++) mean += flux[i];
        mean /= envLen;
        for (int i = 0; i < envLen; i++) flux[i] -= (float)mean;

        int minLag = (int)Math.Ceiling(60.0 * sampleRate / (HOP * 200.0));
        int maxLag = (int)Math.Floor(60.0 * sampleRate / (HOP * 60.0));
        maxLag = Math.Min(maxLag, envLen - 2);

        double best = -1;
        int bestLag = 0;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            int len = envLen - lag;
            if (len < 1) break;
            double num = 0, d1 = 0, d2 = 0;
            for (int i = 0; i < len; i++)
            {
                num += flux[i] * flux[i + lag];
                d1 += flux[i] * flux[i];
                d2 += flux[i + lag] * flux[i + lag];
            }
            double corr = d1 * d2 > 0 ? num / Math.Sqrt(d1 * d2) : 0;
            if (corr > best) { best = corr; bestLag = lag; }
        }

        if (best < 0.1 || bestLag <= 0) return null;

        double bpm = 60.0 * sampleRate / (HOP * bestLag);
        // Octave correction: keep the tempo in the 90-180 range (most dance music)
        while (bpm < 90) bpm *= 2;
        while (bpm > 180) bpm /= 2;
        return Math.Round(bpm);
    }

    public void Dispose()
    {
        _detectTimer?.Dispose();
        if (_capture != null)
        {
            try { _capture.StopRecording(); } catch { }
            _capture.Dispose();
        }
    }
}
