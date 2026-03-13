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

		string? currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId != null)
		{
			LoadTrackInfo(view, currentTrackId);
		}

		Action<string> onTrackChanged = trackId =>
			Application.MainLoop.Invoke(() => LoadTrackInfo(view, trackId));
		_playbackQueue.OnTrackChanged += onTrackChanged;

		view.OnClose = () => _playbackQueue.OnTrackChanged -= onTrackChanged;
		view.Show();
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
