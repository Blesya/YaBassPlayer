using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Прикладной сценарий запуска «Моей волны»: персональной радиостанции
/// или станции по текущему играющему треку.
/// </summary>
public sealed class ShowMyWaveUseCase
{
	private readonly IMyWavePresenter _myWavePresenter;
	private readonly IMyWaveWindowPresenter _myWaveWindowPresenter;
	private readonly IPlaybackPresenter _playbackPresenter;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly IPlayStatusPresenter _playStatusPresenter;
	private readonly IPlaybackQueue _playbackQueue;

	public ShowMyWaveUseCase(
		IMyWavePresenter myWavePresenter,
		IMyWaveWindowPresenter myWaveWindowPresenter,
		IPlaybackPresenter playbackPresenter,
		IPlaylistsPresenter playlistsPresenter,
		IPlayStatusPresenter playStatusPresenter,
		IPlaybackQueue playbackQueue)
	{
		_myWavePresenter = myWavePresenter;
		_myWaveWindowPresenter = myWaveWindowPresenter;
		_playbackPresenter = playbackPresenter;
		_playlistsPresenter = playlistsPresenter;
		_playStatusPresenter = playStatusPresenter;
		_playbackQueue = playbackQueue;
	}

	public async Task ShowAsync(Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		var playlist = await _myWavePresenter.StartMyWaveAsync();
		if (playlist is null) return;

		Activate(playlist, setWindowTitle);
		Logging.LogAfterCall();
	}

	public async Task ShowByTrackAsync(Action<string> setWindowTitle)
	{
		Logging.LogBeforeCall();

		var trackId = _playbackQueue.CurrentTrackId;
		if (trackId == null)
		{
			_playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
			return;
		}

		var playlist = await _myWavePresenter.StartMyWaveFromTrackAsync(trackId);
		if (playlist is null) return;

		Activate(playlist, setWindowTitle);
		Logging.LogAfterCall();
	}

	private void Activate(Playlist playlist, Action<string> setWindowTitle)
	{
		_playbackPresenter.SetPlaylistType(PlaylistType.MyWave);
		setWindowTitle($"{playlist.PlaylistName} : {playlist.Description}");
		_playlistsPresenter.NotifyTransientPlaylistActive(playlist);
		_myWaveWindowPresenter.ShowWindow(playlist);
	}
}
