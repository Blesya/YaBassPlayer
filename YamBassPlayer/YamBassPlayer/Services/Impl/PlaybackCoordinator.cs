using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;

namespace YamBassPlayer.Services.Impl;

public sealed class PlaybackCoordinator(
	ITrackFileProvider trackFileProvider,
	IPlaybackQueue playbackQueue,
	ITrackInfoProvider trackInfoProvider,
	IListenTimer listenTimer,
	IAudioPlayer audioPlayer,
	IPlayStatusPresenter playStatusPresenter,
	IMyWavePresenter myWavePresenter)
	: IPlaybackCoordinator
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

			playStatusPresenter.SetTitle($"Загружается трек: {track.Artist} - {track.Title}");
			string filePath = await trackFileProvider.DownloadTrackAsync(trackId);
			if (string.IsNullOrWhiteSpace(filePath))
				return;

			playStatusPresenter.SetPlayStatus($"Сейчас играет: {track.Artist} - {track.Title}");
			Console.Title = $"{track.Artist} - {track.Title}";
			audioPlayer.Play(filePath);

			var source = _currentPlaylistType switch
			{
				PlaylistType.OnSameWave => ListenSource.OnSameWave,
				PlaylistType.MyWave => ListenSource.MyWave,
				_ => ListenSource.Regular
			};

			listenTimer.OnTrackStart(trackId, source);
			playStatusPresenter.SetCurrentTrackId(trackId);

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
			playStatusPresenter.SetTitle($"Предзагрузка: {nextTrack.Artist} - {nextTrack.Title}");
			await trackFileProvider.DownloadTrackAsync(nextTrackId);
		}
		finally
		{
			playStatusPresenter.SetTitle("Управление воспроизведением");
		}
	}
}
