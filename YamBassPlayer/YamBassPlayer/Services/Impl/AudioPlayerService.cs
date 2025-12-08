using ManagedBass;
using Terminal.Gui;
using YamBassPlayer.Extensions;

namespace YamBassPlayer.Services.Impl;

public class AudioPlayerService : IAudioPlayer
{
    private int _currentStream;

    public event EventHandler? OnTrackEnded;

    public bool IsPlayed => Bass.ChannelIsActive(_currentStream) == PlaybackState.Playing;

    public void Init()
    {
        if (!Bass.Init())
        {
            MessageBox.ErrorQuery("Ошибка", "Не удалось инициализировать BASS", "OK");
        }
    }

    public void Play(string filePath, string trackName = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            if (_currentStream != 0)
            {
                Bass.ChannelStop(_currentStream);
                Bass.StreamFree(_currentStream);
                _currentStream = 0;
            }

            _currentStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);

            if (_currentStream == 0)
            {
                throw new ArgumentNullException(nameof(_currentStream), "Ошибка при попытке воспроизведения, библиотека не инициализирована");
            }

            Bass.ChannelSetSync(
                _currentStream,
                SyncFlags.End,
                0,
                OnBassTrackEnded
            );

            Bass.ChannelPlay(_currentStream);
        }
        catch (Exception exception)
        {
            exception.Handle();
        }
    }

    public int GetProgressInPercent()
    {
        try
        {
            if (_currentStream == 0)
                return 0;

            long pos = Bass.ChannelGetPosition(_currentStream);
            if (pos <= 0)
                return 0;

            long len = Bass.ChannelGetLength(_currentStream);
            if (len <= 0)
                return 0;

            double percent = (double)pos / len * 100.0;

            if (percent < 0)
            {
                percent = 0;
            }

            if (percent > 100)
            {
                percent = 100;
            }

            return (int)percent;
        }
        catch (Exception e)
        {
            e.Handle();
            return 0;
        }
    }

    public float[] ChannelGetData()
    {
        try
        {
            float[] fft = new float[128];

            if (_currentStream == 0)
            {
                return fft;
            }

            Bass.ChannelGetData(_currentStream, fft, (int)DataFlags.FFT256);
            return fft;
        }
        catch (Exception exception)
        {
            exception.Handle();
            return [];
        }
    }

    public void SeekToPercent(int percent)
    {
        try
        {
            if (_currentStream == 0)
                return;

            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            long length = Bass.ChannelGetLength(_currentStream);
            if (length <= 0)
                return;

            long newPos = (long)(length * (percent / 100.0));

            Bass.ChannelSetPosition(_currentStream, newPos);
        }
        catch (Exception exception)
        {
            exception.Handle();
        }
    }

    public void Pause()
    {
        if (_currentStream != 0)
        {
            Bass.ChannelPause(_currentStream);
        }
    }

    public void Resume()
    {
        if (_currentStream != 0)
        {
            Bass.ChannelPlay(_currentStream);
        }
    }

    private void OnBassTrackEnded(int handle, int channel, int data, IntPtr user)
    {
        OnTrackEnded?.Invoke(null, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_currentStream != 0)
        {
            Bass.ChannelStop(_currentStream);
            Bass.StreamFree(_currentStream);
            _currentStream = 0;
        }
    }

    public void Free()
    {
        Stop();
        Bass.Free();
    }
}