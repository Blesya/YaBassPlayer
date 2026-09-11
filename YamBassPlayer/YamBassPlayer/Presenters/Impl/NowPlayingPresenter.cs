using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class NowPlayingPresenter : INowPlayingPresenter
{
	private const int SpectrumRefreshMs = 16;
	private const int WaveformSampleCount = 512;

	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackCatalog _trackCatalog;
	private readonly IEventBus _eventBus;
	private readonly IViewFactory _viewFactory;
	private readonly IModalWindowHost _modalWindowHost;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IErrorHandler _errorHandler;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public NowPlayingPresenter(
		IAudioPlayer audioPlayer,
		IPlaybackQueue playbackQueue,
		ITrackCatalog trackCatalog,
		IEventBus eventBus,
		IViewFactory viewFactory,
		IModalWindowHost modalWindowHost,
		IUiDispatcher uiDispatcher,
		IErrorHandler errorHandler)
	{
		_audioPlayer = audioPlayer;
		_playbackQueue = playbackQueue;
		_trackCatalog = trackCatalog;
		_eventBus = eventBus;
		_viewFactory = viewFactory;
		_modalWindowHost = modalWindowHost;
		_uiDispatcher = uiDispatcher;
		_errorHandler = errorHandler;
	}

	public void ShowNowPlaying()
	{
		var view = _viewFactory.Create<INowPlayingView>();

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
			LoadTrackInfo(view, currentTrackId).Forget();

		_onTrackChangedHandler = e =>
			_uiDispatcher.Invoke(() => LoadTrackInfo(view, e.TrackId).Forget());
		_eventBus.Subscribe(_onTrackChangedHandler);

		bool alive = true;
		_uiDispatcher.StartTimer(TimeSpan.FromMilliseconds(SpectrumRefreshMs), () =>
		{
			if (!alive) return false;
			if (!_audioPlayer.IsPlayed) return true;

			view.SetSpectrumData(
				view.SpectrumDataType == SpectrumDataType.Waveform
					? _audioPlayer.GetWaveformData(WaveformSampleCount)
					: _audioPlayer.ChannelGetData());
			return true;
		});

		try
		{
			_modalWindowHost.Show(view);
		}
		finally
		{
			alive = false;
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

	private async Task LoadTrackInfo(INowPlayingView view, string trackId)
	{
		try
		{
			Track track = await _trackCatalog.GetAsync(trackId);
			view.SetTrack(track);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}
}
