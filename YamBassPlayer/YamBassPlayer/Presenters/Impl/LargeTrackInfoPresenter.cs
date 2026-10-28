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

	private const int CoverReloadDelayMs = 150;

	private TrackKey? _currentTrackKey;
	private CancellationTokenSource? _coverReloadCts;
	private int _coverVersion;

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

		_currentTrackKey = null;

		Action onCoverSizeChanged = () => ReloadCoverAfterResize(view);
		view.CoverSizeChanged += onCoverSizeChanged;

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
			view.CoverSizeChanged -= onCoverSizeChanged;
			CancelCoverReload();

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
		_currentTrackKey = key;
		int version = ++_coverVersion;

		try
		{
			Track track = await _trackCatalog.GetAsync(key);
			view.SetTrack(track);

			CoverArt? coverArt = await RenderCoverAsync(track, view.CoverSize);
			if (version == _coverVersion)
				view.SetCover(coverArt);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private async Task<CoverArt?> RenderCoverAsync(Track track, (int Width, int Height) size)
	{
		string coverPath = await _coverProvider.DownloadCoverAsync(track);
		return await _coverArtService.RenderAsync(coverPath, size.Width, size.Height);
	}

	/// <summary>
	/// Окно изменило доступный размер обложки. Рендер тяжёлый, а при перетаскивании
	/// окна событие приходит десятки раз, поэтому берём паузу и оставляем только
	/// последний запрос.
	/// </summary>
	private void ReloadCoverAfterResize(ILargeTrackInfoView view)
	{
		if (_currentTrackKey is not { } key)
			return;

		CancelCoverReload();
		var cts = new CancellationTokenSource();
		_coverReloadCts = cts;
		ReloadCoverAsync(view, key, cts.Token).Forget();
	}

	private async Task ReloadCoverAsync(ILargeTrackInfoView view, TrackKey key, CancellationToken ct)
	{
		try
		{
			await Task.Delay(CoverReloadDelayMs, ct);

			Track track = await _trackCatalog.GetAsync(key);
			int version = ++_coverVersion;
			CoverArt? coverArt = await RenderCoverAsync(track, view.CoverSize);
			ct.ThrowIfCancellationRequested();

			if (version == _coverVersion)
				view.SetCover(coverArt);
		}
		catch (OperationCanceledException)
		{
			// Окно закрыли или размер сменился ещё раз — этот рендер не нужен.
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private void CancelCoverReload()
	{
		// Поздний рендер прошлого окна не должен перезаписать обложку нового.
		_coverVersion++;

		_coverReloadCts?.Cancel();
		_coverReloadCts?.Dispose();
		_coverReloadCts = null;
	}
}
