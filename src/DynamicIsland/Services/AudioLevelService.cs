using DynamicIsland.Core;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace DynamicIsland.Services;

/// <summary>
/// Listens to what the speakers play (WASAPI loopback — not the microphone) and exposes
/// 4 band levels for the equalizer. Runs only between Start() and Stop().
/// </summary>
public sealed class AudioLevelService : IDisposable
{
    private const int Hop = AudioBands.FrameSize / 2;
    private const long StaleMs = 150;
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly object _gate = new();
    private readonly float[] _ring = new float[AudioBands.FrameSize];
    private readonly float[] _frame = new float[AudioBands.FrameSize];
    private int _write, _sinceCompute;
    private double[] _latest = new double[AudioBands.BandCount];
    private long _latestAt;
    private WasapiLoopbackCapture? _capture;
    private bool _wanted;
    private MMDeviceEnumerator? _enumerator;
    private DeviceNotifier? _notifier;

    public void Start()
    {
        WasapiLoopbackCapture? old;
        lock (_gate)
        {
            if (_wanted) return;
            _wanted = true;
            EnsureNotifier();
            old = StartCaptureLocked();
        }
        DisposeCapture(old);
    }

    public void Stop()
    {
        WasapiLoopbackCapture? old;
        lock (_gate)
        {
            if (!_wanted) return;
            _wanted = false;
            old = DetachLocked();
        }
        DisposeCapture(old);
    }

    /// <summary>Latest levels (0..1 per band); zeros when no recent audio; null when capture is unavailable.</summary>
    public double[]? TryGetLevels()
    {
        lock (_gate)
        {
            if (_capture is null) return null;
            if (Environment.TickCount64 - _latestAt > StaleMs) return new double[AudioBands.BandCount];
            return (double[])_latest.Clone();
        }
    }

    /// <summary>Starts a fresh capture; returns the previous one, which the caller disposes OUTSIDE the lock
    /// (NAudio's Dispose joins the capture thread, which may be waiting on this lock in OnData).</summary>
    private WasapiLoopbackCapture? StartCaptureLocked()
    {
        var old = DetachLocked();
        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnStopped;
            capture.StartRecording();
            _capture = capture;
            _write = _sinceCompute = 0;
            Array.Clear(_ring);
        }
        catch (Exception ex)
        {
            Log.Error("AudioLevel.Start", ex);
            _capture = null;
        }
        return old;
    }

    private WasapiLoopbackCapture? DetachLocked()
    {
        var c = _capture;
        _capture = null;
        if (c is null) return null;
        c.DataAvailable -= OnData;
        c.RecordingStopped -= OnStopped;
        return c;
    }

    private static void DisposeCapture(WasapiLoopbackCapture? c)
    {
        if (c is null) return;
        try { c.StopRecording(); c.Dispose(); }
        catch (Exception ex) { Log.Error("AudioLevel.Dispose", ex); }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture c) return;
        var fmt = c.WaveFormat;
        var channels = Math.Max(1, fmt.Channels);
        var isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat ||
                      (fmt is WaveFormatExtensible x && x.SubFormat == IeeeFloatSubFormat);
        var bytesPerSample = fmt.BitsPerSample / 8;
        if (bytesPerSample == 0) return;
        var frameBytes = bytesPerSample * channels;

        lock (_gate)
        {
            if (!ReferenceEquals(c, _capture)) return;
            for (var off = 0; off + frameBytes <= e.BytesRecorded; off += frameBytes)
            {
                double sum = 0;
                for (var ch = 0; ch < channels; ch++)
                {
                    var p = off + ch * bytesPerSample;
                    sum += isFloat && bytesPerSample == 4 ? BitConverter.ToSingle(e.Buffer, p)
                         : bytesPerSample == 2 ? BitConverter.ToInt16(e.Buffer, p) / 32768.0
                         : 0;
                }
                _ring[_write] = (float)(sum / channels);
                _write = (_write + 1) % _ring.Length;
                if (++_sinceCompute < Hop) continue;

                _sinceCompute = 0;
                for (var i = 0; i < _frame.Length; i++) _frame[i] = _ring[(_write + i) % _ring.Length];
                _latest = AudioBands.Compute(_frame, fmt.SampleRate);
                _latestAt = Environment.TickCount64;
            }
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is null) return;
        Log.Error("AudioLevel.Stopped", e.Exception);
        _ = RestartLaterAsync(); // e.g. the output device was unplugged
    }

    private async Task RestartLaterAsync()
    {
        await Task.Delay(1500);
        WasapiLoopbackCapture? old = null;
        lock (_gate) { if (_wanted) old = StartCaptureLocked(); }
        DisposeCapture(old);
    }

    private void EnsureNotifier()
    {
        if (_notifier is not null) return;
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _notifier = new DeviceNotifier(() => _ = RestartLaterAsync());
            _enumerator.RegisterEndpointNotificationCallback(_notifier);
        }
        catch (Exception ex) { Log.Error("AudioLevel.Notifier", ex); }
    }

    public void Dispose()
    {
        Stop();
        try
        {
            if (_notifier is not null) _enumerator?.UnregisterEndpointNotificationCallback(_notifier);
            _enumerator?.Dispose();
        }
        catch { }
    }

    /// <summary>Restarts capture when the default output device changes (e.g. headphones plugged in).</summary>
    private sealed class DeviceNotifier(Action onDefaultRenderChanged) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia) onDefaultRenderChanged();
        }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
