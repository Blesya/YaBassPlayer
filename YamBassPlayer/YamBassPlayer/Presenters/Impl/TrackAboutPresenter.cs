using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class TrackAboutPresenter : ITrackAboutPresenter
{
	private readonly ITrackAboutService _trackAboutService;
	private readonly IViewFactory _viewFactory;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IErrorHandler _errorHandler;

	private int _isShowing;

	public TrackAboutPresenter(
		ITrackAboutService trackAboutService,
		IViewFactory viewFactory,
		IUiDispatcher uiDispatcher,
		IPlaybackQueue playbackQueue,
		IErrorHandler errorHandler)
	{
		_trackAboutService = trackAboutService;
		_viewFactory = viewFactory;
		_uiDispatcher = uiDispatcher;
		_playbackQueue = playbackQueue;
		_errorHandler = errorHandler;
	}

	public void ShowCurrentTrackAbout()
	{
		string? trackId = _playbackQueue.CurrentTrackId;
		if (string.IsNullOrWhiteSpace(trackId))
			return;

		ShowTrackAbout(trackId);
	}

	public void ShowTrackAbout(string trackId)
		=> ShowTrackAboutAsync(trackId, forceRefresh: true).Forget();

	public async Task ShowTrackAboutAsync(string trackId, bool forceRefresh = false)
	{
		if (Interlocked.CompareExchange(ref _isShowing, 1, 0) != 0)
			return;

		try
		{
			TrackAboutInfo info = await _trackAboutService.GetAsync(trackId, forceRefresh);

			_uiDispatcher.Invoke(() =>
			{
				try
				{
					ITrackAboutView view = _viewFactory.Create<ITrackAboutView>();
					view.SetInfo(info);
					view.Show();
				}
				finally
				{
					// Сбрасываем только после закрытия окна: иначе повторный F7
					// может открыть второе окно поверх первого.
					Interlocked.Exchange(ref _isShowing, 0);
				}
			});
		}
		catch (Exception ex)
		{
			Interlocked.Exchange(ref _isShowing, 0);
			_errorHandler.Handle(ex);
		}
	}
}
