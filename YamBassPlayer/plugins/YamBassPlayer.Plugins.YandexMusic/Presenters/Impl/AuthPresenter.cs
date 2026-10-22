using System.Diagnostics;
using Terminal.Gui;
using YamBassPlayer.Plugins;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Оркестрирует вход: получает код для страницы Яндекса и опрашивает подтверждение,
/// запускает получение QR-кода, проверяет введённый токен и сохраняет результат в конфиг.
/// </summary>
public sealed class AuthPresenter : IAuthPresenter
{
	private const int ErrorTolerance = 3;

	private readonly IAuthService _authService;
	private readonly IQrCodeRenderer _qrCodeRenderer;
	private readonly IAuthView _view;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IErrorHandler _errorHandler;
	private readonly IPluginSettings _appConfiguration;
	private readonly TimeSpan _pollInterval;
	private readonly TimeSpan _qrLifetime;

	private CancellationTokenSource? _polling;
	private bool _authorized;

	public AuthPresenter(
		IAuthService authService,
		IQrCodeRenderer qrCodeRenderer,
		IAuthView view,
		IUiDispatcher uiDispatcher,
		IErrorHandler errorHandler,
		IPluginSettings appConfiguration,
		TimeSpan? pollInterval = null,
		TimeSpan? qrLifetime = null)
	{
		_authService = authService;
		_qrCodeRenderer = qrCodeRenderer;
		_view = view;
		_uiDispatcher = uiDispatcher;
		_errorHandler = errorHandler;
		_appConfiguration = appConfiguration;
		_pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
		_qrLifetime = qrLifetime ?? TimeSpan.FromMinutes(5);
	}

	/// <inheritdoc/>
	public bool Authorized => _authorized;

	/// <inheritdoc/>
	public bool Run()
	{
		Attach();
		try
		{
			Application.Run(_view.ModalWindow);
		}
		finally
		{
			Detach();
		}

		return _authorized;
	}

	/// <summary>Подписывается на события вьюхи и запускает первый способ входа.</summary>
	public void Attach()
	{
		_view.OnMethodSelected += HandleMethodSelected;
		_view.OnTokenSubmitted += HandleTokenSubmitted;
		_view.OnDeviceRefreshRequested += HandleDeviceRefresh;
		_view.OnQrRefreshRequested += HandleQrRefresh;
		_view.OnOpenLinkRequested += HandleOpenLink;
		_view.OnCancelRequested += HandleCancel;

		_view.SetMethods([AuthMethod.Device, AuthMethod.Qr, AuthMethod.Token]);
		StartDeviceFlowAsync().Forget();
	}

	/// <summary>Отписывается от событий и останавливает опрос.</summary>
	public void Detach()
	{
		_view.OnMethodSelected -= HandleMethodSelected;
		_view.OnTokenSubmitted -= HandleTokenSubmitted;
		_view.OnDeviceRefreshRequested -= HandleDeviceRefresh;
		_view.OnQrRefreshRequested -= HandleQrRefresh;
		_view.OnOpenLinkRequested -= HandleOpenLink;
		_view.OnCancelRequested -= HandleCancel;

		CancelPolling();
	}

	/// <summary>Запрашивает код для входа на сайте и опрашивает подтверждение.</summary>
	public Task StartDeviceFlowAsync()
	{
		CancelPolling();
		_polling = new CancellationTokenSource();
		return RunDeviceFlowAsync(_polling.Token);
	}

	/// <summary>Получает новый QR-код и опрашивает подтверждение входа.</summary>
	public Task StartQrFlowAsync()
	{
		CancelPolling();
		_polling = new CancellationTokenSource();
		return RunQrFlowAsync(_polling.Token);
	}

	/// <summary>Проверяет введённый токен и при успехе закрывает окно.</summary>
	public async Task SubmitTokenAsync(string token)
	{
		try
		{
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(true);
				_view.SetStatus("Проверяем токен…");
			});

			bool ok = await _authService.AuthorizeAsync(token);
			if (ok)
			{
				_appConfiguration.Set("token", token);
				CompleteAuthorization();
				return;
			}

			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(false);
				_view.SetStatus("Токен не подошёл. Проверьте его и попробуйте снова.", isError: true);
			});
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(false);
				_view.SetStatus($"Ошибка: {ex.Message}", isError: true);
			});
		}
	}

	private void HandleMethodSelected(AuthMethod method)
	{
		switch (method)
		{
			case AuthMethod.Device:
				StartDeviceFlowAsync().Forget();
				return;
			case AuthMethod.Qr:
				StartQrFlowAsync().Forget();
				return;
			default:
				CancelPolling();
				_view.SetStatus("Вставьте токен Яндекс.Музыки и нажмите «Войти».");
				return;
		}
	}

	private void HandleDeviceRefresh() => StartDeviceFlowAsync().Forget();

	private void HandleQrRefresh() => StartQrFlowAsync().Forget();

	private void HandleCancel()
	{
		CancelPolling();
		_view.Close();
	}

	private void HandleTokenSubmitted(string token) => SubmitTokenAsync(token).Forget();

	private void HandleOpenLink(string link)
	{
		if (string.IsNullOrWhiteSpace(link))
			return;

		try
		{
			Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	private void CancelPolling()
	{
		_polling?.Cancel();
		_polling?.Dispose();
		_polling = null;
	}

	private async Task RunDeviceFlowAsync(CancellationToken token)
	{
		try
		{
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(true);
				_view.SetStatus("Получаем код для входа…");
			});

			DeviceCodeChallenge? challenge = await _authService.StartDeviceAuthorizationAsync();
			if (token.IsCancellationRequested)
				return;

			if (challenge is null)
			{
				_uiDispatcher.Invoke(() =>
				{
					_view.SetBusy(false);
					_view.SetStatus("Не удалось получить код. Проверьте связь и нажмите «Новый код».", isError: true);
				});
				return;
			}

			_uiDispatcher.Invoke(() =>
			{
				bool copied = _view.ShowDeviceCode(challenge.UserCode, challenge.VerificationUrl);
				_view.SetBusy(false);
				_view.SetStatus(copied
					? $"Код скопирован в буфер обмена. Вставьте его на {challenge.VerificationUrl}."
					: $"Откройте {challenge.VerificationUrl} и введите код.");
			});

			await PollDeviceAsync(challenge, token);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(false);
				_view.SetStatus($"Ошибка входа по коду: {ex.Message}", isError: true);
			});
		}
	}

	private async Task PollDeviceAsync(DeviceCodeChallenge challenge, CancellationToken token)
	{
		DateTime deadline = DateTime.UtcNow + challenge.Lifetime;
		int consecutiveErrors = 0;

		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(challenge.Interval, token);
			}
			catch (TaskCanceledException)
			{
				return;
			}

			if (token.IsCancellationRequested)
				return;

			DeviceAuthResult result = await _authService.CheckDeviceAuthorizationAsync();
			if (token.IsCancellationRequested)
				return;

			switch (result.State)
			{
				case DeviceAuthState.Authorized:
					CompleteAuthorization(result.AccessToken, result.RefreshToken);
					return;
				case DeviceAuthState.Expired:
					_uiDispatcher.Invoke(() =>
						_view.SetStatus("Код устарел. Нажмите «Новый код».", isError: true));
					return;
				case DeviceAuthState.Denied:
					_uiDispatcher.Invoke(() =>
						_view.SetStatus("Доступ отклонён. Нажмите «Новый код».", isError: true));
					return;
				case DeviceAuthState.Error:
					consecutiveErrors++;
					if (consecutiveErrors == ErrorTolerance)
					{
						_uiDispatcher.Invoke(() =>
							_view.SetStatus("Нет ответа от Яндекса. Продолжаем ждать подтверждения…"));
					}

					break;
				default:
					consecutiveErrors = 0;
					break;
			}

			if (DateTime.UtcNow >= deadline)
			{
				_uiDispatcher.Invoke(() =>
					_view.SetStatus("Код устарел. Нажмите «Новый код».", isError: true));
				return;
			}
		}
	}

	private async Task RunQrFlowAsync(CancellationToken token)
	{
		try
		{
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(true);
				_view.SetStatus("Получаем QR-код…");
			});

			string link = await _authService.StartQrAuthorizationAsync();
			if (token.IsCancellationRequested)
				return;

			if (string.IsNullOrWhiteSpace(link))
			{
				// Яндекс изменил страницу входа, и библиотека не смогла получить ссылку.
				// Не оставляем пользователя в тупике — уводим на вход по токену.
				_uiDispatcher.Invoke(() =>
				{
					_view.SetBusy(false);
					_view.SelectMethod(AuthMethod.Token);
					_view.SetStatus("QR-вход сейчас недоступен. Вставьте токен Яндекс.Музыки.", isError: true);
				});
				return;
			}

			string qrText = _qrCodeRenderer.Render(link);
			_uiDispatcher.Invoke(() =>
			{
				_view.ShowQrCode(qrText, link);
				_view.SetBusy(false);
				_view.SetStatus("Отсканируйте код приложением Яндекс и подтвердите вход.");
			});

			await PollQrAsync(token);
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
			_uiDispatcher.Invoke(() =>
			{
				_view.SetBusy(false);
				_view.SetStatus($"Ошибка входа по QR-коду: {ex.Message}", isError: true);
			});
		}
	}

	private async Task PollQrAsync(CancellationToken token)
	{
		DateTime deadline = DateTime.UtcNow + _qrLifetime;
		int consecutiveErrors = 0;

		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_pollInterval, token);
			}
			catch (TaskCanceledException)
			{
				return;
			}

			if (token.IsCancellationRequested)
				return;

			QrAuthState state = await _authService.CheckQrAuthorizationAsync();
			if (token.IsCancellationRequested)
				return;

			if (state == QrAuthState.Authorized)
			{
				CompleteAuthorization();
				return;
			}

			if (state == QrAuthState.Error)
			{
				consecutiveErrors++;
				if (consecutiveErrors == ErrorTolerance)
				{
					_uiDispatcher.Invoke(() =>
						_view.SetStatus("Нет ответа от Яндекса. Продолжаем ждать подтверждения…"));
				}
			}
			else
			{
				consecutiveErrors = 0;
			}

			if (DateTime.UtcNow >= deadline)
			{
				_uiDispatcher.Invoke(() =>
					_view.SetStatus("Код устарел. Нажмите «Новый код».", isError: true));
				return;
			}
		}
	}

	private void CompleteAuthorization(string? token = null, string? refreshToken = null)
	{
		_authorized = true;

		string? accessToken = string.IsNullOrWhiteSpace(token) ? TryGetToken() : token;
		if (!string.IsNullOrWhiteSpace(accessToken))
			_appConfiguration.Set("token", accessToken);

		if (!string.IsNullOrWhiteSpace(refreshToken))
			_appConfiguration.Set("refreshToken", refreshToken);

		_uiDispatcher.Invoke(() =>
		{
			_view.SetStatus("Вход выполнен.");
			_view.Close();
		});
	}

	private string? TryGetToken()
	{
		try
		{
			return _authService.Storage.Token;
		}
		catch
		{
			return null;
		}
	}
}
