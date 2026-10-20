using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Presenter orchestration for playback: coordinates track load → status update → radio notification.
/// Previously lived in the service layer (<c>PlaybackCoordinator</c>), which inverted the MVP layers.
/// </summary>
public sealed class PlaybackPresenter(
	ITrackFileProvider trackFileProvider,
	IPlaybackQueue playbackQueue,
	ITrackInfoProvider trackInfoProvider,
	IListenTimer listenTimer,
	IAudioPlayer audioPlayer,
	IPlayStatusPresenter playStatusPresenter,
	IMyWavePresenter myWavePresenter,
	ITrackAboutPresenter trackAboutPresenter)
	: IPlaybackPresenter
{
	private PlaylistType _currentPlaylistType = PlaylistType.Favorite;
	private string? _currentMyWaveTrackId;
	private bool _myWaveSkipPending;

	/// <summary>
	/// Номер последнего запроса на воспроизведение. Растёт при каждом вызове
	/// <see cref="PlaySelectedTrackAsync"/>, поэтому устаревшие (перекрытые) запросы
	/// не запускают воспроизведение и не перетирают статус.
	/// </summary>
	private int _playRequestVersion;

	/// <summary>
	/// Синхронизирует фиксацию «кто сейчас играет»: проверка актуальности запроса и
	/// сам запуск воспроизведения выполняются как одна атомарная операция.
	/// </summary>
	private readonly object _playLock = new();

	/// <summary>
	/// Сколько недоступных треков подряд пропущено при автопереходе. Сбрасывается,
	/// когда удаётся запустить трек; защищает от бесконечного цикла, если недоступна вся очередь.
	/// </summary>
	private int _consecutiveUnavailable;

	public void SetPlaylistType(PlaylistType playlistType)
	{
		_currentPlaylistType = playlistType;
		if (playlistType != PlaylistType.MyWave)
		{
			_currentMyWaveTrackId = null;
			_myWaveSkipPending = false;
		}
	}

	public void MarkMyWaveSkipPending()
	{
		if (_currentPlaylistType == PlaylistType.MyWave)
			_myWaveSkipPending = true;
	}

	public async Task PlaySelectedTrackAsync(string trackId, bool isAutomatic = false)
	{
		Logging.LogBeforeCall();

		int requestVersion = Interlocked.Increment(ref _playRequestVersion);

		try
		{
			Track track = await trackInfoProvider.GetTrackInfoById(trackId);
			if (!IsCurrentRequest(requestVersion))
				return;

			string downloadTitle = $"Загружается трек: {track.Artist} - {track.Title}";
			playStatusPresenter.SetTitle(downloadTitle);
			var downloadProgress = new ActionProgress<DownloadProgress>(progress =>
			{
				if (!IsCurrentRequest(requestVersion))
					return;

				string formatted = FormatDownloadProgress(progress);
				playStatusPresenter.SetTitle($"{downloadTitle} ({formatted})");
				playStatusPresenter.SetPlayStatus($"Загрузка: {formatted}");
			});

			string filePath;
			try
			{
				filePath = await trackFileProvider.DownloadTrackAsync(trackId, downloadProgress);
			}
			catch (TrackUnavailableException)
			{
				if (IsCurrentRequest(requestVersion))
					await HandleUnavailableTrackAsync(track, isAutomatic);

				return;
			}

			if (string.IsNullOrWhiteSpace(filePath))
				return;

			// Пока качалось, пользователь мог выбрать другой трек — тогда этот запрос
			// больше не должен ничего воспроизводить (загрузка остаётся в кэше).
			// Проверка и запуск под одним замком, чтобы устаревший запрос не проскочил
			// между ними и не перебил более новый.
			lock (_playLock)
			{
				if (!IsCurrentRequest(requestVersion))
					return;

				NotifyMyWavePreviousTrack();

				playStatusPresenter.SetPlayStatus($"Сейчас играет: {track.Artist} - {track.Title}");
				Console.Title = $"{track.Artist} - {track.Title}";
				audioPlayer.Play(filePath);
				_consecutiveUnavailable = 0;

				var source = _currentPlaylistType switch
				{
					PlaylistType.MyWave => ListenSource.MyWave,
					_ => ListenSource.Regular
				};

				listenTimer.OnTrackStart(trackId, source);
				playStatusPresenter.SetCurrentTrack(track.Id, track.SourceType);

				if (_currentPlaylistType == PlaylistType.MyWave)
				{
					_currentMyWaveTrackId = trackId;
					_ = myWavePresenter.NotifyTrackStartedAsync(trackId);

					if (!playbackQueue.HasNext)
						_ = myWavePresenter.FetchMoreTracksAsync();
				}
			}
		}
		finally
		{
			if (IsCurrentRequest(requestVersion))
				playStatusPresenter.SetTitle("Управление воспроизведением");
			Logging.LogAfterCall();
		}
	}

	public async Task PreloadNextTrackAsync()
	{
		Logging.LogBeforeCall();

		int requestVersion = Volatile.Read(ref _playRequestVersion);

		try
		{
			var nextTrackId = playbackQueue.PeekNextTrackId;
			if (nextTrackId == null || trackFileProvider.IsTrackDownloaded(nextTrackId))
				return;

			Track nextTrack = await trackInfoProvider.GetTrackInfoById(nextTrackId);
			if (requestVersion != Volatile.Read(ref _playRequestVersion))
				return;

			string preloadTitle = $"Предзагрузка: {nextTrack.Artist} - {nextTrack.Title}";
			playStatusPresenter.SetTitle(preloadTitle);
			var downloadProgress = new ActionProgress<DownloadProgress>(progress =>
			{
				if (requestVersion != Volatile.Read(ref _playRequestVersion))
					return;

				playStatusPresenter.SetTitle($"{preloadTitle} ({FormatDownloadProgress(progress)})");
			});
			try
			{
				await trackFileProvider.DownloadTrackAsync(nextTrackId, downloadProgress);
			}
			catch (TrackUnavailableException)
			{
				// При явном выборе трека окно «О треке» откроет основной путь;
				// предзагрузка просто не мешает играть текущий трек.
			}
		}
		finally
		{
			if (requestVersion == Volatile.Read(ref _playRequestVersion))
				playStatusPresenter.SetTitle("Управление воспроизведением");
			Logging.LogAfterCall();
		}
	}

	private async Task HandleUnavailableTrackAsync(Track track, bool isAutomatic)
	{
		if (!isAutomatic)
		{
			playStatusPresenter.SetPlayStatus("Трек недоступен");
			await trackAboutPresenter.ShowTrackAboutAsync(track.Id, forceRefresh: true);
			return;
		}

		// Автопереход: окно не открываем, просто пробуем следующий трек.
		int queueSize = Math.Max(1, playbackQueue.TrackIds.Count);
		if (++_consecutiveUnavailable < queueSize)
		{
			playStatusPresenter.SetPlayStatus($"Пропуск недоступного трека: {track.Artist} - {track.Title}");
			playbackQueue.NextAutomatic();
			return;
		}

		_consecutiveUnavailable = 0;
		playStatusPresenter.SetPlayStatus("Нет доступных треков");
	}

	private bool IsCurrentRequest(int version) => version == Volatile.Read(ref _playRequestVersion);

	/// <summary>
	/// Уведомляет «Мою волну» о судьбе предыдущего трека. Вызывается только перед
	/// фактическим стартом нового воспроизведения, чтобы устаревшие запросы не
	/// отправляли ложные уведомления.
	/// </summary>
	private void NotifyMyWavePreviousTrack()
	{
		if (_currentPlaylistType != PlaylistType.MyWave || _currentMyWaveTrackId == null)
			return;

		double played = audioPlayer.GetCurrentPosition().TotalSeconds;
		if (_myWaveSkipPending)
			_ = myWavePresenter.NotifyTrackSkippedAsync(_currentMyWaveTrackId, played);
		else
			_ = myWavePresenter.NotifyTrackFinishedAsync(_currentMyWaveTrackId, played);

		_myWaveSkipPending = false;
	}

	private static string FormatDownloadProgress(DownloadProgress progress)
		=> progress.HasTotal
			? $"{progress.Percent}%"
			: progress.BytesReceived.ToHumanReadableSize();

	/// <summary>
	/// Reports progress synchronously on the calling thread (unlike <see cref="Progress{T}"/>,
	/// which posts to a captured context), so the UI updates stay ordered with download completion.
	/// </summary>
	private sealed class ActionProgress<T>(Action<T> onReport) : IProgress<T>
	{
		public void Report(T value) => onReport(value);
	}
}
