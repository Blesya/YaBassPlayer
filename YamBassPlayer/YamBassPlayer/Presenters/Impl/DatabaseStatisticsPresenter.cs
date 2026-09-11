using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class DatabaseStatisticsPresenter : IDatabaseStatisticsPresenter
{
	private readonly IDatabaseStatisticsService _service;
	private readonly IViewFactory _viewFactory;

	public DatabaseStatisticsPresenter(IDatabaseStatisticsService service, IViewFactory viewFactory)
	{
		_service = service;
		_viewFactory = viewFactory;
	}

	public void ShowStatisticsDialog()
	{
		var stats = _service.CollectStatistics();
		var view = _viewFactory.Create<IDatabaseStatisticsView>();
		view.SetStatistics(stats);
		view.Show();
	}
}
