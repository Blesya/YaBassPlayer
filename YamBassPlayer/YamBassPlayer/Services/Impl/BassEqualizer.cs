using ManagedBass;
using ManagedBass.Fx;

namespace YamBassPlayer.Services.Impl;

public class BassEqualizer : IBassEqualizer
{
    private readonly float[] _gains = new float[10];
    private readonly int[] _handles = new int[10];
    private int _stream;

    private static readonly float[] Freqs =
    [
        32, 64, 125, 250, 500,
        1000, 2000, 4000, 8000, 16000
    ];

    public BassEqualizer()
    {
        Version? version = BassFx.Version;
        if (version == null)
            throw new NullReferenceException();
    }

    public void SetBand(int index, float gain)
    {
        _gains[index] = gain;

        if (_stream != 0)
        {
            ApplyBand(index);
        }
    }

    public void AttachToStream(int stream)
    {
        _stream = stream;

        Array.Clear(_handles);

        for (int i = 0; i < _gains.Length; i++)
        {
            ApplyBand(i);
        }
    }

    private void ApplyBand(int i)
    {
        if (_stream == 0)
            return;

        if (_handles[i] == 0)
        {
            _handles[i] = Bass.ChannelSetFX(_stream, EffectType.PeakEQ, 0);
            if (_handles[i] == 0)
                return;
        }

        var peakEqParameters = new PeakEQParameters
        {
            lBand = 0,
            fCenter = Freqs[i],
            fBandwidth = 1.0f,
            fGain = _gains[i] * 10f
        };

        Bass.FXSetParameters(_handles[i], peakEqParameters);
    }
}