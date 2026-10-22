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
	private readonly ICoverArtService _coverArtService;
	private readonly ILyricsService _lyricsService;
	private readonly ITrackCatalog _trackCatalog;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IEventBus _eventBus;
	private readonly IErrorHandler _errorHandler;
	private readonly Action<TrackChangedEvent> _onTrackChangedHandler;
	private TrackKey? _loadingTrack;

	public TrackInfoPanelPresenter(
		ITrackInfoPanelView view,
		ICoverProvider coverProvider,
		ICoverArtService coverArtService,
		ILyricsService lyricsService,
		ITrackCatalog trackCatalog,
		IPlaybackQueue playbackQueue,
		IEventBus eventBus,
		IErrorHandler errorHandler)
	{
		_view = view;
		_coverProvider = coverProvider;
		_coverArtService = coverArtService;
		_lyricsService = lyricsService;
		_trackCatalog = trackCatalog;
		_playbackQueue = playbackQueue;
		_eventBus = eventBus;
		_errorHandler = errorHandler;

		_onTrackChangedHandler = e => ShowTrack(e.Key).Forget();
		_eventBus.Subscribe(_onTrackChangedHandler);

		if (_playbackQueue.CurrentTrack is { } currentTrack)
			ShowTrack(currentTrack).Forget();
	}

	private async Task ShowTrack(TrackKey key)
	{
		if (key == _loadingTrack)
			return;
		_loadingTrack = key;

		try
		{
			Track track = await _trackCatalog.GetAsync(key);
			_view.SetTrack(track);

			string coverPath = await _coverProvider.DownloadCoverAsync(track);
			CoverArt? coverArt = await _coverArtService.RenderAsync(coverPath, _view.CoverSize.Width, _view.CoverSize.Height);
			_view.SetCover(coverArt);

			string? lyrics = await _lyricsService.GetLyricsAsync(track);
			_view.SetLyrics(lyrics);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_view.SetLyrics(null);
		}
	}
}
