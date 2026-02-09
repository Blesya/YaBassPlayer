using YamBassPlayer.Services;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class DatabaseStatisticsPresenter : IDatabaseStatisticsPresenter
{
	private readonly IDatabaseStatisticsService _service;

	public DatabaseStatisticsPresenter(IDatabaseStatisticsService service)
	{
		_service = service;
	}

	public void ShowStatisticsDialog()
	{
		var stats = _service.CollectStatistics();
		var view = new DatabaseStatisticsView();
		view.SetStatistics(stats);
		view.Show();
	}
}
