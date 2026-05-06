using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Следит за сменой воспроизводимого трека и отображает его в панели «Инфо».
/// </summary>
public sealed class TrackInfoPanelPresenter : ITrackInfoPanelPresenter
{
	private readonly ITrackInfoPanelView _view;
	private readonly ICoverProvider _coverProvider;
	private readonly ILyricsService _lyricsService;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IEventBus _eventBus;
	private readonly Action<TrackChangedEvent> _onTrackChangedHandler;
	private string? _loadingTrackId;

	public TrackInfoPanelPresenter(
		ITrackInfoPanelView view,
		ICoverProvider coverProvider,
		ILyricsService lyricsService,
		ITrackInfoProvider trackInfoProvider,
		IPlaybackQueue playbackQueue,
		IEventBus eventBus)
	{
		_view = view;
		_coverProvider = coverProvider;
		_lyricsService = lyricsService;
		_trackInfoProvider = trackInfoProvider;
		_playbackQueue = playbackQueue;
		_eventBus = eventBus;

		_onTrackChangedHandler = e => ShowTrack(e.TrackId);
		_eventBus.Subscribe(_onTrackChangedHandler);

		if (_playbackQueue.CurrentTrackId is { } currentTrackId)
			ShowTrack(currentTrackId);
	}

	private async void ShowTrack(string trackId)
	{
		if (trackId == _loadingTrackId)
			return;
		_loadingTrackId = trackId;

		try
		{
			Track track = await _trackInfoProvider.GetTrackInfoById(trackId);
			_view.SetTrack(track);

			string coverPath = await _coverProvider.DownloadCoverAsync(track.Id);
			_view.SetCover(string.IsNullOrWhiteSpace(coverPath) ? null : coverPath);

			string? lyrics = await _lyricsService.GetLyricsAsync(track);
			_view.SetLyrics(lyrics);
		}
		catch (Exception ex)
		{
			ex.Handle();
			_view.SetLyrics(null);
		}
	}
}
