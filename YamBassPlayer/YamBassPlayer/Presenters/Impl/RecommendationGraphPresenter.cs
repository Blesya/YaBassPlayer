using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Services;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class RecommendationGraphPresenter : IRecommendationGraphPresenter
{
	private readonly IRecommendationService _recommendationService;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IPlayStatusPresenter _playStatusPresenter;

	public RecommendationGraphPresenter(
		IRecommendationService recommendationService,
		IPlaybackQueue playbackQueue,
		IPlayStatusPresenter playStatusPresenter)
	{
		_recommendationService = recommendationService;
		_playbackQueue = playbackQueue;
		_playStatusPresenter = playStatusPresenter;
	}

	public void ShowRecommendationGraph()
	{
		var currentTrackId = _playbackQueue.CurrentTrackId;
		if (currentTrackId == null)
		{
			_playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
			return;
		}

		var view = new RecommendationGraphView();

		Application.MainLoop.AddTimeout(TimeSpan.Zero, _ =>
		{
			LoadGraphData(view, currentTrackId);
			return false;
		});

		view.Show();
	}

	private async void LoadGraphData(RecommendationGraphView view, string trackId)
	{
		try
		{
			var data = await _recommendationService.GetGraphDataAsync(trackId);
			view.SetGraphData(data);
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
