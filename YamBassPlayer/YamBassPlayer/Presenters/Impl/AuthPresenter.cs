using System.Diagnostics;
using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Оркестрирует вход: запускает получение QR-кода, опрашивает подтверждение,
/// проверяет введённый токен и сохраняет результат в конфиг.
/// </summary>
public sealed class AuthPresenter : IAuthPresenter
{
	private const int ErrorTolerance = 3;

	private readonly IAuthService _authService;
	private readonly IQrCodeRenderer _qrCodeRenderer;
	private readonly IAuthView _view;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IErrorHandler _errorHandler;
	private readonly IAppConfiguration _appConfiguration;
	private readonly TimeSpan _pollInterval;
	private readonly TimeSpan _qrLifetime;

	private CancellationTokenSource? _qrPolling;
	private bool _authorized;

	public AuthPresenter(
		IAuthService authService,
		IQrCodeRenderer qrCodeRenderer,
		IAuthView view,
		IUiDispatcher uiDispatcher,
		IErrorHandler errorHandler,
		IAppConfiguration appConfiguration,
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
		_view.OnQrRefreshRequested += HandleQrRefresh;
		_view.OnOpenLinkRequested += HandleOpenLink;
		_view.OnCancelRequested += HandleCancel;

		_view.SetMethods([AuthMethod.Qr, AuthMethod.Token]);
		StartQrFlowAsync().Forget();
	}

	/// <summary>Отписывается от событий и останавливает опрос QR.</summary>
	public void Detach()
	{
		_view.OnMethodSelected -= HandleMethodSelected;
		_view.OnTokenSubmitted -= HandleTokenSubmitted;
		_view.OnQrRefreshRequested -= HandleQrRefresh;
		_view.OnOpenLinkRequested -= HandleOpenLink;
		_view.OnCancelRequested -= HandleCancel;

		CancelQrPolling();
	}

	/// <summary>Получает новый QR-код и опрашивает подтверждение входа.</summary>
	public Task StartQrFlowAsync()
	{
		CancelQrPolling();
		_qrPolling = new CancellationTokenSource();
		return RunQrFlowAsync(_qrPolling.Token);
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
				_appConfiguration.SaveToken(token);
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
		if (method == AuthMethod.Qr)
		{
			StartQrFlowAsync().Forget();
			return;
		}

		CancelQrPolling();
		_view.SetStatus("Вставьте токен Яндекс.Музыки и нажмите «Войти».");
	}

	private void HandleQrRefresh() => StartQrFlowAsync().Forget();

	private void HandleCancel()
	{
		CancelQrPolling();
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

	private void CancelQrPolling()
	{
		_qrPolling?.Cancel();
		_qrPolling?.Dispose();
		_qrPolling = null;
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

	private void CompleteAuthorization()
	{
		_authorized = true;

		string? token = TryGetToken();
		if (!string.IsNullOrWhiteSpace(token))
			_appConfiguration.SaveToken(token);

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
