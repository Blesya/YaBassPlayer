using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class NowPlayingPresenter : INowPlayingPresenter
{
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly PlayStatusView _playStatusView;

	public NowPlayingPresenter(
		IAudioPlayer audioPlayer,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		PlayStatusView playStatusView)
	{
		_audioPlayer = audioPlayer;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_playStatusView = playStatusView;
	}

	public void ShowNowPlaying()
	{
		var view = new NowPlayingView();

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
			LoadTrackInfo(view, currentTrackId);

		Action<string> onTrackChanged = trackId =>
			Application.MainLoop.Invoke(() => LoadTrackInfo(view, trackId));
		_playbackQueue.OnTrackChanged += onTrackChanged;

		bool alive = true;
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (!alive) return false;
			view.SetFftData(_audioPlayer.ChannelGetData());
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
		_playbackQueue.OnTrackChanged -= onTrackChanged;
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
