using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class NowPlayingPresenter : INowPlayingPresenter
{
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly PlayStatusView _playStatusView;
	private readonly IEventBus _eventBus;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public NowPlayingPresenter(
		IAudioPlayer audioPlayer,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		PlayStatusView playStatusView,
		IEventBus eventBus)
	{
		_audioPlayer = audioPlayer;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_playStatusView = playStatusView;
		_eventBus = eventBus;
	}

	public void ShowNowPlaying()
	{
		var view = new NowPlayingView();

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
			LoadTrackInfo(view, currentTrackId);

		_onTrackChangedHandler = e =>
			Application.MainLoop.Invoke(() => LoadTrackInfo(view, e.TrackId));
		_eventBus.Subscribe(_onTrackChangedHandler);

		bool alive = true;
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (!alive) return false;
			if (!_audioPlayer.IsPlayed) return true;
			view.SetSpectrumData(
				view.SpectrumDataType == SpectrumDataType.Waveform
					? _audioPlayer.GetWaveformData(512)
					: _audioPlayer.ChannelGetData());
			return true;
		});

		View? originalParent = _playStatusView.SuperView;
		originalParent?.Remove(_playStatusView);
		_playStatusView.Y = Pos.AnchorEnd(5);
		view.Add(_playStatusView);

		view.OnClose = () =>
		{
			view.Remove(_playStatusView);
			_playStatusView.Y = Pos.AnchorEnd(5);
			originalParent?.Add(_playStatusView);
			originalParent?.SetNeedsDisplay();
		};

		view.Show();

		alive = false;
		if (_onTrackChangedHandler is not null)
		{
			_eventBus.Unsubscribe(_onTrackChangedHandler);
			_onTrackChangedHandler = null;
		}
	}

	private async void LoadTrackInfo(NowPlayingView view, string trackId)
	{
		try
		{
			Track track = await _trackInfoProvider.GetTrackInfoById(trackId);
			view.SetTrack(track);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
