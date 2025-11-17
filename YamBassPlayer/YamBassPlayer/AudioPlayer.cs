using ManagedBass;

using Terminal.Gui;

namespace YamBassPlayer
{
	internal static class AudioPlayer
	{
		private static int _currentStream;
		public static string CurrentTrackName { get; private set; } = "Нет воспроизведения";

		public static event EventHandler? OnTrackEnded;

		public static void Init()
		{
			if (!Bass.Init())
			{
				MessageBox.ErrorQuery("Ошибка", "Не удалось инициализировать BASS", "OK");
			}
		}

		public static void Play(string filePath, string trackName = "")
		{
			try
			{
				// Останавливаем предыдущий поток, если есть
				if (_currentStream != 0)
				{
					Bass.ChannelStop(_currentStream);
					Bass.StreamFree(_currentStream);
					_currentStream = 0;
				}

				_currentStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);

				if (_currentStream == 0)
				{
					MessageBox.ErrorQuery("Ошибка", $"Не удалось создать поток для: {trackName}", "OK");
					return;
				}

				Bass.ChannelSetSync(
					_currentStream,
					SyncFlags.End,
					0,
					OnBassTrackEnded
				);

				Bass.ChannelPlay(_currentStream);
				CurrentTrackName = string.IsNullOrEmpty(trackName) ? "Неизвестный трек" : trackName;
			}
			catch (Exception ex)
			{
				MessageBox.ErrorQuery("Ошибка", $"Не удалось воспроизвести трек:\n{ex.Message}", "OK");
			}
		}

		private static void OnBassTrackEnded(int handle, int channel, int data, IntPtr user)
		{
			// Вызываем .NET-событие
			OnTrackEnded?.Invoke(null, EventArgs.Empty);
		}

		public static void Stop()
		{
			if (_currentStream != 0)
			{
				Bass.ChannelStop(_currentStream);
				Bass.StreamFree(_currentStream);
				_currentStream = 0;
				CurrentTrackName = "Нет воспроизведения";
			}
		}

		public static void Free()
		{
			Stop();
			Bass.Free();
		}
	}
}
