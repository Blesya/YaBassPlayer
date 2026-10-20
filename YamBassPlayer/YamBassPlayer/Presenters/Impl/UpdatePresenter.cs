using Serilog;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Constants;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Сценарий обновления. Проверка идёт в фоне, окно и загрузка — на UI-потоке:
/// Terminal.Gui не терпит обращений к вьюхам из чужих потоков.
/// </summary>
public sealed class UpdatePresenter : IUpdatePresenter
{
	private readonly IAudioPlayer _audioPlayer;
	private readonly IAppConfiguration _configuration;
	private readonly IUpdateDialogView _dialogView;
	private readonly IUpdateInstaller _installer;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IUpdateService _updateService;

	private int _checking;
	private bool _dialogVisible;
	private volatile Version? _postponedVersion;

	public UpdatePresenter(
		IUpdateService updateService,
		IUpdateDialogView dialogView,
		IUpdateInstaller installer,
		IAudioPlayer audioPlayer,
		IUiDispatcher uiDispatcher,
		IAppConfiguration configuration)
	{
		_updateService = updateService;
		_dialogView = dialogView;
		_installer = installer;
		_audioPlayer = audioPlayer;
		_uiDispatcher = uiDispatcher;
		_configuration = configuration;
	}

	public void CheckForUpdates(bool manual)
	{
		if (!manual && !_configuration.GetUpdatesEnabled())
			return;

		if (Interlocked.Exchange(ref _checking, 1) == 1)
			return;

		Task.Run(async () =>
		{
			try
			{
				await CheckAsync(manual);
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Не удалось проверить обновления");

				if (manual)
					_uiDispatcher.Invoke(() => _dialogView.ShowError($"Не удалось проверить обновления: {exception.Message}"));
			}
			finally
			{
				Interlocked.Exchange(ref _checking, 0);
			}
		});
	}

	/// <summary>Проверка без фоновой обвязки: используется тестами.</summary>
	internal async Task CheckAsync(bool manual)
	{
		UpdateInfo? info;

		try
		{
			info = await _updateService.CheckAsync(CancellationToken.None);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось проверить обновления");

			if (manual)
				_uiDispatcher.Invoke(() => _dialogView.ShowError($"Не удалось проверить обновления: {exception.Message}"));

			return;
		}

		if (info is null || !IsNewer(info.Version))
		{
			if (manual)
				_uiDispatcher.Invoke(() => _dialogView.ShowUpToDate(AppInfo.Version));

			return;
		}

		// Ту же версию, которую пользователь уже отложил, повторно не предлагаем.
		if (!manual && _postponedVersion == info.Version)
			return;

		_uiDispatcher.Invoke(() => ShowUpdateDialog(info));
	}

	private void ShowUpdateDialog(UpdateInfo info)
	{
		if (_dialogVisible)
			return;

		_dialogVisible = true;

		try
		{
			UpdateDialogResult result = _dialogView.Show(
				info,
				AppInfo.Version,
				(progress, cancellationToken) => _updateService.DownloadAsync(info, progress, cancellationToken));

			if (result.Decision != UpdateDecision.Install || string.IsNullOrEmpty(result.ZipPath))
			{
				_postponedVersion = info.Version;
				return;
			}

			Log.Information("Установка обновления {Version}", info.Version);
			Logging.LogUserAction($"установка обновления {info.Version}");

			_audioPlayer.Free();
			_installer.Schedule(result.ZipPath!, info.Version.ToString());
			Application.RequestStop();
		}
		finally
		{
			_dialogVisible = false;
		}
	}

	private static bool IsNewer(Version version) => AppInfo.ParsedVersion is not null && version > AppInfo.ParsedVersion;
}
