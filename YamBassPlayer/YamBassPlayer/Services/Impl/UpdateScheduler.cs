using YamBassPlayer.Configuration;
using YamBassPlayer.Presenters;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Проверка обновлений по расписанию: через несколько секунд после старта (чтобы не лезть
/// поверх сплэша) и затем раз в час. Таймеры живут в UI-цикле, сеть уходит в фон.
/// </summary>
public sealed class UpdateScheduler : IUpdateScheduler
{
	private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(8);
	private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

	private readonly IAppConfiguration _configuration;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IUpdatePresenter _updatePresenter;
	private bool _started;

	public UpdateScheduler(IUpdatePresenter updatePresenter, IUiDispatcher uiDispatcher, IAppConfiguration configuration)
	{
		_updatePresenter = updatePresenter;
		_uiDispatcher = uiDispatcher;
		_configuration = configuration;
	}

	public void Start()
	{
		if (_started)
			return;

		_started = true;

		_uiDispatcher.StartTimer(FirstCheckDelay, () =>
		{
			CheckSilently();
			return false;
		});

		_uiDispatcher.StartTimer(CheckInterval, () =>
		{
			CheckSilently();
			return true;
		});
	}

	private void CheckSilently()
	{
		if (!_configuration.GetUpdatesEnabled())
			return;

		_updatePresenter.CheckForUpdates(manual: false);
	}
}
