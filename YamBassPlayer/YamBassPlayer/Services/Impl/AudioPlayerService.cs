using ManagedBass;
using Terminal.Gui;
using YamBassPlayer.Extensions;

namespace YamBassPlayer.Services.Impl;

public class AudioPlayerService(IBassEqualizer bassEqualizer) : IAudioPlayer
{
	private int _currentStream;
	private const double PreloadSecondsBeforeEnd = 30.0;
	
	public event EventHandler? OnTrackEnded;
	public event EventHandler? OnPreloadRequested;

	public bool IsPlayed =>
		Bass.ChannelIsActive(_currentStream) == PlaybackState.Playing;

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

			Stop();

			_currentStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
			if (_currentStream == 0)
			{
				throw new Exception("Не удалось создать аудиопоток");
			}

			Bass.ChannelSetSync(_currentStream, SyncFlags.End, 0, OnBassTrackEnded);
			SetupPreloadSync();
			bassEqualizer.AttachToStream(_currentStream);
			Bass.ChannelPlay(_currentStream);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	public void Pause()
	{
		if (IsStreamActive)
		{
			Bass.ChannelPause(_currentStream);
		}
	}

	public void Resume()
	{
		if (IsStreamActive)
		{
			Bass.ChannelPlay(_currentStream);
		}
	}

	public void Stop()
	{
		if (!IsStreamActive)
		{
			return;
		}

		Bass.ChannelStop(_currentStream);
		Bass.StreamFree(_currentStream);
		_currentStream = 0;
	}

	public void Free()
	{
		Stop();
		Bass.Free();
	}

	private bool IsStreamActive => _currentStream != 0;

	private void OnBassTrackEnded(int handle, int channel, int data, IntPtr user)
	{
		OnTrackEnded?.Invoke(this, EventArgs.Empty);
	}

	private void SetupPreloadSync()
	{
		try
		{
			long len = Bass.ChannelGetLength(_currentStream);
			if (len <= 0)
			{
				return;
			}

			double duration = Bass.ChannelBytes2Seconds(_currentStream, len);
			if (duration <= PreloadSecondsBeforeEnd)
			{
				return;
			}

			double preloadTime = duration - PreloadSecondsBeforeEnd;
			long preloadPos = Bass.ChannelSeconds2Bytes(_currentStream, preloadTime);

			Bass.ChannelSetSync(_currentStream, SyncFlags.Position, preloadPos, OnPreloadSync);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private void OnPreloadSync(int handle, int channel, int data, IntPtr user)
	{
		OnPreloadRequested?.Invoke(this, EventArgs.Empty);
	}

	public int GetProgressInPercent()
	{
		try
		{
			if (!IsStreamActive)
			{
				return 0;
			}

			long pos = Bass.ChannelGetPosition(_currentStream);
			long len = Bass.ChannelGetLength(_currentStream);

			if (pos <= 0 || len <= 0)
			{
				return 0;
			}

			return (int)Math.Clamp((double)pos / len * 100.0, 0, 100);
		}
		catch (Exception e)
		{
			e.Handle();
			return 0;
		}
	}

	public TimeSpan GetCurrentPosition()
	{
		try
		{
			if (!IsStreamActive)
			{
				return TimeSpan.Zero;
			}

			long pos = Bass.ChannelGetPosition(_currentStream);
			return pos < 0
				? TimeSpan.Zero
				: TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_currentStream, pos));
		}
		catch (Exception e)
		{
			e.Handle();
			return TimeSpan.Zero;
		}
	}

	public TimeSpan GetDuration()
	{
		try
		{
			if (!IsStreamActive)
			{
				return TimeSpan.Zero;
			}

			long len = Bass.ChannelGetLength(_currentStream);
			return len < 0
				? TimeSpan.Zero
				: TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_currentStream, len));
		}
		catch (Exception e)
		{
			e.Handle();
			return TimeSpan.Zero;
		}
	}

	public void SeekToPercent(int percent)
	{
		try
		{
			if (!IsStreamActive)
			{
				return;
			}

			percent = Math.Clamp(percent, 0, 100);

			long len = Bass.ChannelGetLength(_currentStream);
			if (len <= 0)
			{
				return;
			}

			long newPos = (long)(len * (percent / 100.0));
			Bass.ChannelSetPosition(_currentStream, newPos);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	public float[] ChannelGetData()
	{
		try
		{
			float[] fft = new float[128];
			if (!IsStreamActive)
			{
				return fft;
			}

			Bass.ChannelGetData(_currentStream, fft, (int)DataFlags.FFT256);
			return fft;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	public void SetEqualizerBand(int bandIndex, float gain)
		=> bassEqualizer.SetBand(bandIndex, gain);
}
