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
	IMyWavePresenter myWavePresenter)
	: IPlaybackPresenter
{
	private PlaylistType _currentPlaylistType = PlaylistType.Favorite;
	private string? _currentMyWaveTrackId;
	private bool _myWaveSkipPending;

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

	public async Task PlaySelectedTrackAsync(string trackId)
	{
		try
		{
			if (_currentPlaylistType == PlaylistType.MyWave && _currentMyWaveTrackId != null)
			{
				double played = audioPlayer.GetCurrentPosition().TotalSeconds;
				if (_myWaveSkipPending)
					_ = myWavePresenter.NotifyTrackSkippedAsync(_currentMyWaveTrackId, played);
				else
					_ = myWavePresenter.NotifyTrackFinishedAsync(_currentMyWaveTrackId, played);

				_myWaveSkipPending = false;
			}

			Track track = await trackInfoProvider.GetTrackInfoById(trackId);

			string downloadTitle = $"Загружается трек: {track.Artist} - {track.Title}";
			playStatusPresenter.SetTitle(downloadTitle);
			var downloadProgress = new ActionProgress<DownloadProgress>(progress =>
			{
				string formatted = FormatDownloadProgress(progress);
				playStatusPresenter.SetTitle($"{downloadTitle} ({formatted})");
				playStatusPresenter.SetPlayStatus($"Загрузка: {formatted}");
			});
			string filePath = await trackFileProvider.DownloadTrackAsync(trackId, downloadProgress);
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			playStatusPresenter.SetPlayStatus($"Сейчас играет: {track.Artist} - {track.Title}");
			Console.Title = $"{track.Artist} - {track.Title}";
			audioPlayer.Play(filePath);

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
		finally
		{
			playStatusPresenter.SetTitle("Управление воспроизведением");
		}
	}

	public async Task PreloadNextTrackAsync()
	{
		try
		{
			var nextTrackId = playbackQueue.PeekNextTrackId;
			if (nextTrackId == null || trackFileProvider.IsTrackDownloaded(nextTrackId))
				return;

			Track nextTrack = await trackInfoProvider.GetTrackInfoById(nextTrackId);
			string preloadTitle = $"Предзагрузка: {nextTrack.Artist} - {nextTrack.Title}";
			playStatusPresenter.SetTitle(preloadTitle);
			var downloadProgress = new ActionProgress<DownloadProgress>(progress =>
				playStatusPresenter.SetTitle($"{preloadTitle} ({FormatDownloadProgress(progress)})"));
			await trackFileProvider.DownloadTrackAsync(nextTrackId, downloadProgress);
		}
		finally
		{
			playStatusPresenter.SetTitle("Управление воспроизведением");
		}
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
