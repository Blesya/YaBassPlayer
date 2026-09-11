using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class MyWaveWindowPresenter : IMyWaveWindowPresenter
{
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackCatalog _trackCatalog;
	private readonly ICoverProvider _coverProvider;
	private readonly ICoverArtService _coverArtService;
	private readonly IEventBus _eventBus;
	private readonly IViewFactory _viewFactory;
	private readonly IModalWindowHost _modalWindowHost;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IErrorHandler _errorHandler;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public MyWaveWindowPresenter(
		IPlaybackQueue playbackQueue,
		ITrackCatalog trackCatalog,
		ICoverProvider coverProvider,
		ICoverArtService coverArtService,
		IEventBus eventBus,
		IViewFactory viewFactory,
		IModalWindowHost modalWindowHost,
		IUiDispatcher uiDispatcher,
		IErrorHandler errorHandler)
	{
		_playbackQueue = playbackQueue;
		_trackCatalog = trackCatalog;
		_coverProvider = coverProvider;
		_coverArtService = coverArtService;
		_eventBus = eventBus;
		_viewFactory = viewFactory;
		_modalWindowHost = modalWindowHost;
		_uiDispatcher = uiDispatcher;
		_errorHandler = errorHandler;
	}

	public void ShowWindow(Playlist playlist)
	{
		var view = _viewFactory.Create<IMyWaveView>();
		view.SetWaveDescription(playlist.Description ?? "Персональная радиостанция");

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
		{
			LoadTrackInfo(view, currentTrackId).Forget();
			UpdateNextTrackLabel(view).Forget();
		}

		_onTrackChangedHandler = e => _uiDispatcher.Invoke(() =>
		{
			LoadTrackInfo(view, e.TrackId).Forget();
			UpdateNextTrackLabel(view).Forget();
		});
		_eventBus.Subscribe(_onTrackChangedHandler);

		try
		{
			_modalWindowHost.Show(view);
		}
		finally
		{
			UnsubscribeTrackChanged();
		}
	}

	private void UnsubscribeTrackChanged()
	{
		if (_onTrackChangedHandler is not null)
		{
			_eventBus.Unsubscribe(_onTrackChangedHandler);
			_onTrackChangedHandler = null;
		}
	}

	private async Task LoadTrackInfo(IMyWaveView view, string trackId)
	{
		try
		{
			Track track = await _trackCatalog.GetAsync(trackId);
			view.SetTrack(track);
			view.SetCover(null);

			string coverPath = await _coverProvider.DownloadCoverAsync(trackId);
			CoverArt? coverArt = await _coverArtService.RenderAsync(coverPath, view.CoverSize.Width, view.CoverSize.Height);
			view.SetCover(coverArt);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task UpdateNextTrackLabel(IMyWaveView view)
	{
		try
		{
			string? nextId = _playbackQueue.PeekNextTrackId;
			if (nextId == null)
			{
				view.SetNextTrackLabel(null);
				return;
			}

			Track next = await _trackCatalog.GetAsync(nextId);
			view.SetNextTrackLabel($"{next.Artist} — {next.Title}");
		}
		catch
		{
			view.SetNextTrackLabel(null);
		}
	}
}
