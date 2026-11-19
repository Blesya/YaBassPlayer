using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class DatabaseStatisticsPresenter : IDatabaseStatisticsPresenter
{
	private readonly IDatabaseStatisticsService _service;
	private readonly IListeningStatisticsService _listeningService;
	private readonly IViewFactory _viewFactory;

	public DatabaseStatisticsPresenter(
		IDatabaseStatisticsService service,
		IListeningStatisticsService listeningService,
		IViewFactory viewFactory)
	{
		_service = service;
		_listeningService = listeningService;
		_viewFactory = viewFactory;
	}

	public void ShowStatisticsDialog()
	{
		var stats = _service.CollectStatistics();
		var listening = _listeningService.Collect();
		var view = _viewFactory.Create<IDatabaseStatisticsView>();
		view.SetStatistics(stats, listening);
		view.Show();
	}
}
