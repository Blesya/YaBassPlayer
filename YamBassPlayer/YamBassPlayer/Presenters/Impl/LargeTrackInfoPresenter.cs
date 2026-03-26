using Autofac;
using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class LargeTrackInfoPresenter : ILargeTrackInfoPresenter
{
	private readonly IPlaybackQueue _playbackQueue;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly IHistoryService _historyService;
	private readonly ICoverProvider _coverProvider;

	public LargeTrackInfoPresenter(
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		IHistoryService historyService,
		ICoverProvider coverProvider)
	{
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_historyService = historyService;
		_coverProvider = coverProvider;
	}

	public void ShowLargeTrackInfo()
	{
		var view = ServicesProvider.Ioc.Resolve<ILargeTrackInfoView>();

		LoadPlaylistAsync(view);

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
		{
			view.SetCurrentTrackId(currentTrackId);
			LoadTrackInfo(view, currentTrackId);
		}

		Action<string> onTrackChanged = trackId =>
			Application.MainLoop.Invoke(() =>
			{
				view.SetCurrentTrackId(trackId);
				LoadTrackInfo(view, trackId);
			});
		_playbackQueue.OnTrackChanged += onTrackChanged;

		view.OnTrackActivated = trackId =>
		{
			var trackIds = _playbackQueue.TrackIds;
			int idx = -1;
			for (int i = 0; i < trackIds.Count; i++)
			{
				if (trackIds[i] == trackId)
				{
					idx = i;
					break;
				}
			}
			if (idx >= 0)
				_playbackQueue.SetQueue(trackIds.ToList(), idx);
		};

		view.OnClose = () =>
		{
			_playbackQueue.OnTrackChanged -= onTrackChanged;
			view.OnTrackActivated = null;
		};
		view.Show();
	}

	private async void LoadPlaylistAsync(ILargeTrackInfoView view)
	{
		try
		{
			var trackIds = _playbackQueue.TrackIds;
			if (trackIds.Count == 0)
				return;

			var tracks = await _trackInfoProvider.GetTracksInfoByIds(trackIds);
			view.SetPlaylist(tracks.ToList().AsReadOnly());
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}

	private async void LoadTrackInfo(ILargeTrackInfoView view, string trackId)
	{
		try
		{
			Track track = await _trackInfoProvider.GetTrackInfoById(trackId);
			view.SetTrack(track);
			view.SetListenCount(_historyService.GetListenCount(trackId));

			string coverPath = await _coverProvider.DownloadCoverAsync(trackId);
			view.SetCover(string.IsNullOrWhiteSpace(coverPath) ? null : coverPath);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
