using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class LargeTrackInfoPresenter : ILargeTrackInfoPresenter
{
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackCatalog _trackCatalog;
	private readonly ICoverProvider _coverProvider;
	private readonly ICoverArtService _coverArtService;
	private readonly IViewFactory _viewFactory;
	private readonly IEventBus _eventBus;
	private readonly IErrorHandler _errorHandler;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public LargeTrackInfoPresenter(
		IPlaybackQueue playbackQueue,
		ITrackCatalog trackCatalog,
		ICoverProvider coverProvider,
		ICoverArtService coverArtService,
		IViewFactory viewFactory,
		IEventBus eventBus,
		IErrorHandler errorHandler)
	{
		_playbackQueue = playbackQueue;
		_trackCatalog = trackCatalog;
		_coverProvider = coverProvider;
		_coverArtService = coverArtService;
		_viewFactory = viewFactory;
		_eventBus = eventBus;
		_errorHandler = errorHandler;
	}

	public void ShowLargeTrackInfo()
	{
		var view = _viewFactory.Create<ILargeTrackInfoView>();

		LoadPlaylistAsync(view).Forget();

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
		{
			view.SetCurrentTrackId(currentTrackId);
			LoadTrackInfo(view, currentTrackId).Forget();
		}

		_onTrackChangedHandler = e =>
			Application.MainLoop.Invoke(() =>
			{
				view.SetCurrentTrackId(e.TrackId);
				LoadTrackInfo(view, e.TrackId).Forget();
			});
		_eventBus.Subscribe(_onTrackChangedHandler);

		view.OnTrackActivated = trackId =>
		{
			var trackIds = _playbackQueue.TrackIds;
			int idx = -1;
			for (int i = 0; i < trackIds.Count; i++)
			{
				if (trackIds[i] == trackId)
				{
					idx = i;
					break;
				}
			}
			if (idx >= 0)
				_playbackQueue.SetQueue(trackIds.ToList(), idx);
		};

		view.OnClose = () =>
		{
			if (_onTrackChangedHandler is not null)
			{
				_eventBus.Unsubscribe(_onTrackChangedHandler);
				_onTrackChangedHandler = null;
			}
			view.OnTrackActivated = null;
		};
		view.Show();
	}

	private async Task LoadPlaylistAsync(ILargeTrackInfoView view)
	{
		try
		{
			var trackIds = _playbackQueue.TrackIds;
			if (trackIds.Count == 0)
				return;

			IReadOnlyList<Track> tracks = await _trackCatalog.GetManyAsync(trackIds);
			view.SetPlaylist(tracks);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task LoadTrackInfo(ILargeTrackInfoView view, string trackId)
	{
		try
		{
			Track track = await _trackCatalog.GetAsync(trackId);
			view.SetTrack(track);

			string coverPath = await _coverProvider.DownloadCoverAsync(trackId);
			CoverArt? coverArt = await _coverArtService.RenderAsync(coverPath, view.CoverSize.Width, view.CoverSize.Height);
			view.SetCover(coverArt);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}
}
