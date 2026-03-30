using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class TrackInfoPanelPresenter : ITrackInfoPanelPresenter
{
	private readonly ITrackInfoPanelView _view;
	private readonly ICoverProvider _coverProvider;
	private readonly ILyricsService _lyricsService;

	public TrackInfoPanelPresenter(
		ITrackInfoPanelView view,
		ICoverProvider coverProvider,
		ILyricsService lyricsService)
	{
		_view = view;
		_coverProvider = coverProvider;
		_lyricsService = lyricsService;
	}

	public void OnTrackSelected(Track track)
	{
		_view.SetTrack(track);
		LoadDetailsAsync(track);
	}

	private async void LoadDetailsAsync(Track track)
	{
		try
		{
			string coverPath = await _coverProvider.DownloadCoverAsync(track.Id);
			_view.SetCover(string.IsNullOrWhiteSpace(coverPath) ? null : coverPath);

			string? lyrics = await _lyricsService.GetLyricsAsync(track);
			_view.SetLyrics(lyrics);
		}
		catch (Exception ex)
		{
			ex.Handle();
			_view.SetLyrics(null);
		}
	}
}
