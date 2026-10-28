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

	private const int CoverReloadDelayMs = 150;

	private TrackKey? _currentTrackKey;
	private CancellationTokenSource? _coverReloadCts;
	private int _coverVersion;

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
		=> _uiDispatcher.Invoke(() => ShowWindowOnUi(playlist));

	/// <summary>
	/// Окно волны открывается модально через <c>Application.Run</c>, а вызывается из
	/// пункта меню плагина, который выполняется в фоновой задаче. Запуск вложенного цикла
	/// Terminal.Gui не на UI-потоке гасит основной цикл и закрывает плеер, поэтому
	/// и создание окна, и его показ идут через диспетчер.
	/// </summary>
	private void ShowWindowOnUi(Playlist playlist)
	{
		var view = _viewFactory.Create<IMyWaveView>();
		view.SetWaveDescription(playlist.Description ?? "Персональная радиостанция");

		_currentTrackKey = null;

		Action onCoverSizeChanged = () => ReloadCoverAfterResize(view);
		view.CoverSizeChanged += onCoverSizeChanged;

		TrackKey? currentTrack = _playbackQueue.CurrentTrack;
		if (currentTrack is not null)
		{
			LoadTrackInfo(view, currentTrack.Value).Forget();
			UpdateNextTrackLabel(view).Forget();
		}

		_onTrackChangedHandler = e => _uiDispatcher.Invoke(() =>
		{
			LoadTrackInfo(view, e.Key).Forget();
			UpdateNextTrackLabel(view).Forget();
		});
		_eventBus.Subscribe(_onTrackChangedHandler);

		try
		{
			_modalWindowHost.Show(view);
		}
		finally
		{
			view.CoverSizeChanged -= onCoverSizeChanged;
			CancelCoverReload();
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

	private async Task LoadTrackInfo(IMyWaveView view, TrackKey key)
	{
		_currentTrackKey = key;
		int version = ++_coverVersion;

		try
		{
			Track track = await _trackCatalog.GetAsync(key);
			view.SetTrack(track);
			view.SetCover(null);

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
	private void ReloadCoverAfterResize(IMyWaveView view)
	{
		if (_currentTrackKey is not { } key)
			return;

		CancelCoverReload();
		var cts = new CancellationTokenSource();
		_coverReloadCts = cts;
		ReloadCoverAsync(view, key, cts.Token).Forget();
	}

	private async Task ReloadCoverAsync(IMyWaveView view, TrackKey key, CancellationToken ct)
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

	private async Task UpdateNextTrackLabel(IMyWaveView view)
	{
		try
		{
			TrackKey? nextKey = _playbackQueue.PeekNext;
			if (nextKey is null)
			{
				view.SetNextTrackLabel(null);
				return;
			}

			Track next = await _trackCatalog.GetAsync(nextKey.Value);
			view.SetNextTrackLabel($"{next.Artist} — {next.Title}");
		}
		catch
		{
			view.SetNextTrackLabel(null);
		}
	}
}
