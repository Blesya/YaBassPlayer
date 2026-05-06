using Autofac;
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
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ICoverProvider _coverProvider;
	private readonly IEventBus _eventBus;
	private Action<TrackChangedEvent>? _onTrackChangedHandler;

	public LargeTrackInfoPresenter(
		IPlaybackQueue playbackQueue,
		ITrackInfoProvider trackInfoProvider,
		ICoverProvider coverProvider,
		IEventBus eventBus)
	{
		_playbackQueue = playbackQueue;
		_trackInfoProvider = trackInfoProvider;
		_coverProvider = coverProvider;
		_eventBus = eventBus;
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

		_onTrackChangedHandler = e =>
			Application.MainLoop.Invoke(() =>
			{
				view.SetCurrentTrackId(e.TrackId);
				LoadTrackInfo(view, e.TrackId);
			});
		_eventBus.Subscribe(_onTrackChangedHandler);

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
			if (_onTrackChangedHandler is not null)
			{
				_eventBus.Unsubscribe(_onTrackChangedHandler);
				_onTrackChangedHandler = null;
			}
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

			string coverPath = await _coverProvider.DownloadCoverAsync(trackId);
			view.SetCover(string.IsNullOrWhiteSpace(coverPath) ? null : coverPath);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
