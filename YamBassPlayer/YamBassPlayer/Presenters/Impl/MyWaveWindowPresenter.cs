using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public sealed class MyWaveWindowPresenter : IMyWaveWindowPresenter
{
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ICoverProvider _coverProvider;
	private readonly PlayStatusView _playStatusView;
	private readonly IEventBus _eventBus;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public MyWaveWindowPresenter(
		IAudioPlayer audioPlayer,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ICoverProvider coverProvider,
		PlayStatusView playStatusView,
		IEventBus eventBus)
	{
		_audioPlayer = audioPlayer;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_coverProvider = coverProvider;
		_playStatusView = playStatusView;
		_eventBus = eventBus;
	}

	public void ShowWindow(Playlist playlist)
	{
		var view = new MyWaveView();
		view.SetWaveDescription(playlist.Description ?? "Персональная радиостанция");

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
		{
			LoadTrackInfoAsync(view, currentTrackId);
			UpdateNextTrackLabel(view);
		}

		_onTrackChangedHandler = e =>
			Application.MainLoop.Invoke(() =>
			{
				LoadTrackInfoAsync(view, e.TrackId);
				UpdateNextTrackLabel(view);
			});
		_eventBus.Subscribe(_onTrackChangedHandler);

		// Заимствуем PlayStatusView (как в NowPlayingPresenter)
		View? originalParent = _playStatusView.SuperView;
		originalParent?.Remove(_playStatusView);
		_playStatusView.Y = Pos.AnchorEnd(5);
		view.Add(_playStatusView);

		view.OnClose = () =>
		{
			UnsubscribeTrackChanged();
			view.Remove(_playStatusView);
			_playStatusView.Y = Pos.AnchorEnd(5);
			originalParent?.Add(_playStatusView);
			originalParent?.SetNeedsDisplay();
		};

		view.Show();

		UnsubscribeTrackChanged();
	}

	private void UnsubscribeTrackChanged()
	{
		if (_onTrackChangedHandler is not null)
		{
			_eventBus.Unsubscribe(_onTrackChangedHandler);
			_onTrackChangedHandler = null;
		}
	}

	private async void LoadTrackInfoAsync(MyWaveView view, string trackId)
	{
		try
		{
			Track track = await _trackInfoProvider.GetTrackInfoById(trackId);
			view.SetTrack(track);
			view.SetCover(null);

			string coverPath = await _coverProvider.DownloadCoverAsync(trackId);
			view.SetCover(string.IsNullOrWhiteSpace(coverPath) ? null : coverPath);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private async void UpdateNextTrackLabel(MyWaveView view)
	{
		try
		{
			string? nextId = _playbackQueue.PeekNextTrackId;
			if (nextId == null)
			{
				view.SetNextTrackLabel(null);
				return;
			}

			Track next = await _trackInfoProvider.GetTrackInfoById(nextId);
			view.SetNextTrackLabel($"{next.Artist} — {next.Title}");
		}
		catch
		{
			view.SetNextTrackLabel(null);
		}
	}
}
