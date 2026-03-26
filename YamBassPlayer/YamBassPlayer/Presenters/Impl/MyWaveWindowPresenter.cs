using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public sealed class MyWaveWindowPresenter : IMyWaveWindowPresenter
{
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ICoverProvider _coverProvider;
	private readonly PlayStatusView _playStatusView;

	public MyWaveWindowPresenter(
		IAudioPlayer audioPlayer,
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ICoverProvider coverProvider,
		PlayStatusView playStatusView)
	{
		_audioPlayer = audioPlayer;
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_coverProvider = coverProvider;
		_playStatusView = playStatusView;
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

		Action<string> onTrackChanged = trackId =>
			Application.MainLoop.Invoke(() =>
			{
				LoadTrackInfoAsync(view, trackId);
				UpdateNextTrackLabel(view);
			});
		_playbackQueue.OnTrackChanged += onTrackChanged;

		bool alive = true;
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (!alive) return false;
			// Обновляем прогресс/время через PlayStatusView — он уже обновляется из PlayStatusPresenter
			return true;
		});

		// Заимствуем PlayStatusView (как в NowPlayingPresenter)
		View? originalParent = _playStatusView.SuperView;
		originalParent?.Remove(_playStatusView);
		_playStatusView.Y = Pos.AnchorEnd(5);
		view.Add(_playStatusView);

		view.OnClose = () =>
		{
			alive = false;
			_playbackQueue.OnTrackChanged -= onTrackChanged;
			view.Remove(_playStatusView);
			_playStatusView.Y = Pos.AnchorEnd(5);
			originalParent?.Add(_playStatusView);
			originalParent?.SetNeedsDisplay();
		};

		view.Show();

		alive = false;
		_playbackQueue.OnTrackChanged -= onTrackChanged;
	}

	private async void LoadTrackInfoAsync(IMyWaveView view, string trackId)
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

	private async void UpdateNextTrackLabel(IMyWaveView view)
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
