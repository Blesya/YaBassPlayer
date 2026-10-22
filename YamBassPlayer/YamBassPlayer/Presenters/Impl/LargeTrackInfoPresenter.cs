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

		TrackKey? currentTrack = _playbackQueue.CurrentTrack;
		if (currentTrack is not null)
		{
			view.SetCurrentTrack(currentTrack.Value);
			LoadTrackInfo(view, currentTrack.Value).Forget();
		}

		_onTrackChangedHandler = e =>
			Application.MainLoop.Invoke(() =>
			{
				view.SetCurrentTrack(e.Key);
				LoadTrackInfo(view, e.Key).Forget();
			});
		_eventBus.Subscribe(_onTrackChangedHandler);

		view.OnTrackActivated = key =>
		{
			var trackKeys = _playbackQueue.TrackKeys;
			int idx = -1;
			for (int i = 0; i < trackKeys.Count; i++)
			{
				if (trackKeys[i] == key)
				{
					idx = i;
					break;
				}
			}
			if (idx >= 0)
				_playbackQueue.SetQueue(trackKeys.ToList(), idx);
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
			var trackKeys = _playbackQueue.TrackKeys;
			if (trackKeys.Count == 0)
				return;

			IReadOnlyList<Track> tracks = await _trackCatalog.GetManyAsync(trackKeys);
			view.SetPlaylist(tracks);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task LoadTrackInfo(ILargeTrackInfoView view, TrackKey key)
	{
		try
		{
			Track track = await _trackCatalog.GetAsync(key);
			view.SetTrack(track);

			string coverPath = await _coverProvider.DownloadCoverAsync(track);
			CoverArt? coverArt = await _coverArtService.RenderAsync(coverPath, view.CoverSize.Width, view.CoverSize.Height);
			view.SetCover(coverArt);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}
}
