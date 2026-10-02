using ManagedBass;
using Serilog;
using Terminal.Gui;
using YamBassPlayer.Extensions;

namespace YamBassPlayer.Services.Impl;

public class AudioPlayerService(IBassEqualizer bassEqualizer) : IAudioPlayer
{
	private int _currentStream;
	private const double PreloadSecondsBeforeEnd = 30.0;
	private float[] _fftBuffer = [];
	private float[] _waveformBuffer = [];
	
	public event EventHandler? OnTrackEnded;
	public event EventHandler? OnPreloadRequested;

	public bool IsPlayed =>
		Bass.ChannelIsActive(_currentStream) == PlaybackState.Playing;

	public void Init()
	{
		Logging.LogBeforeCall();

		if (!Bass.Init())
		{
			Log.Error("Не удалось инициализировать BASS: {Error}", Bass.LastError);
			MessageBox.ErrorQuery("Ошибка", "Не удалось инициализировать BASS", "OK");
		}
		else
		{
			Log.Information("BASS инициализирован");
		}

		Logging.LogAfterCall();
	}

	public void Play(string filePath, string trackName = "")
	{
		Logging.LogBeforeCall();

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

			Log.Information("Воспроизведение: {TrackName} ({FilePath})",
				string.IsNullOrWhiteSpace(trackName) ? filePath : trackName, filePath);
			Logging.LogAfterCall();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Не удалось воспроизвести трек {FilePath}", filePath);
			ex.Handle(logException: false);
		}
	}

	public void Pause()
	{
		Logging.LogBeforeCall();

		if (IsStreamActive)
		{
			Bass.ChannelPause(_currentStream);
		}

		Logging.LogAfterCall();
	}

	public void Resume()
	{
		Logging.LogBeforeCall();

		if (IsStreamActive)
		{
			Bass.ChannelPlay(_currentStream);
		}

		Logging.LogAfterCall();
	}

	public void Stop()
	{
		Logging.LogBeforeCall();

		if (!IsStreamActive)
		{
			return;
		}

		Bass.ChannelStop(_currentStream);
		Bass.StreamFree(_currentStream);
		_currentStream = 0;

		Logging.LogAfterCall();
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
		Logging.LogBeforeCall();

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
			Logging.LogAfterCall();
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
			if (!IsStreamActive)
			{
				return [];
			}

			if (_fftBuffer.Length != 128)
				_fftBuffer = new float[128];

			Bass.ChannelGetData(_currentStream, _fftBuffer, (int)DataFlags.FFT256);
			return _fftBuffer;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}

	public void SetEqualizerBand(int bandIndex, float gain)
		=> bassEqualizer.SetBand(bandIndex, gain);

	public float[] GetWaveformData(int sampleCount = 512)
	{
		try
		{
			if (!IsStreamActive)
				return [];

			if (_waveformBuffer.Length != sampleCount)
				_waveformBuffer = new float[sampleCount];

			Bass.ChannelGetData(_currentStream, _waveformBuffer, (int)DataFlags.Float | (sampleCount * 4));
			return _waveformBuffer;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return [];
		}
	}
}