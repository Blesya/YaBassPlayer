using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Модальное окно «Найдена новая версия». Состояния: предложение загрузить,
/// загрузка с прогрессом и отменой, предложение установить.
/// </summary>
public sealed class UpdateDialog : Dialog
{
	private const int NotesLineLimit = 18;

	private readonly Func<IProgress<int>, CancellationToken, Task<string>> _download;
	private readonly Label _statusLabel;
	private readonly ProgressBar _progressBar;
	private readonly Button _primaryButton;
	private readonly Button _secondaryButton;

	private CancellationTokenSource? _cancellation;
	private string? _downloadedPath;
	private bool _downloading;

	public UpdateDialog(
		UpdateInfo info,
		string currentVersion,
		Func<IProgress<int>, CancellationToken, Task<string>> download)
		: base("Найдена новая версия", 78, 22)
	{
		_download = download;

		var header = new Label
		{
			X = 1,
			Y = 0,
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = $"Доступна версия {info.Version} (у вас {currentVersion})."
		};

		var notes = new TextView
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill() - 2,
			Height = Dim.Fill(6),
			ReadOnly = true,
			WordWrap = true,
			CanFocus = false,
			Text = BuildNotes(info)
		};

		_statusLabel = new Label
		{
			X = 1,
			Y = Pos.AnchorEnd(4),
			Width = Dim.Fill() - 2,
			Height = 1,
			Text = string.Empty
		};

		_progressBar = new ProgressBar
		{
			X = 1,
			Y = Pos.AnchorEnd(3),
			Width = Dim.Fill() - 2,
			Height = 1,
			Fraction = 0f,
			Visible = false
		};

		_primaryButton = new Button("Загрузить") { X = Pos.Center() - 18, Y = Pos.AnchorEnd(1), IsDefault = true };
		_primaryButton.Clicked += OnPrimaryClicked;

		_secondaryButton = new Button("Позже") { X = Pos.Center() + 2, Y = Pos.AnchorEnd(1) };
		_secondaryButton.Clicked += OnSecondaryClicked;

		Add(header, notes, _statusLabel, _progressBar, _primaryButton, _secondaryButton);

		KeyPress += e =>
		{
			if (e.KeyEvent.Key != Key.Esc)
				return;

			Postpone();
			e.Handled = true;
		};
	}

	public UpdateDialogResult Result { get; private set; } = new(UpdateDecision.Postpone, null);

	private void OnPrimaryClicked()
	{
		if (_downloading)
			return;

		if (_downloadedPath is not null)
		{
			Result = new UpdateDialogResult(UpdateDecision.Install, _downloadedPath);
			Application.RequestStop(this);
			return;
		}

		StartDownload();
	}

	private void OnSecondaryClicked()
	{
		if (_downloading)
		{
			_cancellation?.Cancel();
			return;
		}

		Postpone();
	}

	private void StartDownload()
	{
		_downloading = true;
		var cancellation = new CancellationTokenSource();
		_cancellation = cancellation;
		_primaryButton.Text = "Загрузка…";
		_primaryButton.Enabled = false;
		_secondaryButton.Text = "Отмена";
		_statusLabel.Text = "Загрузка архива…";
		_progressBar.Visible = true;
		_progressBar.Fraction = 0f;

		var progress = new InlineProgress(percent => RunOnUi(() =>
		{
			_progressBar.Fraction = percent / 100f;
			_statusLabel.Text = $"Загрузка архива… {percent}%";
		}));

		CancellationToken token = cancellation.Token;
		Task.Run(async () =>
		{
			try
			{
				string path = await _download(progress, token);
				RunOnUi(() => OnDownloaded(path));
			}
			catch (OperationCanceledException)
			{
				RunOnUi(OnDownloadCanceled);
			}
			catch (Exception exception)
			{
				RunOnUi(() => OnDownloadFailed(exception));
			}
		});
	}

	private void OnDownloaded(string path)
	{
		_downloading = false;
		_downloadedPath = path;

		_progressBar.Fraction = 1f;
		_statusLabel.Text = "Архив загружен. «Установить» закроет приложение и перезапустит его.";
		_primaryButton.Text = "Установить";
		_primaryButton.Enabled = true;
		_secondaryButton.Text = "Позже";
	}

	private void OnDownloadCanceled()
	{
		_downloading = false;

		_progressBar.Visible = false;
		_statusLabel.Text = "Загрузка отменена.";
		_primaryButton.Text = "Загрузить";
		_primaryButton.Enabled = true;
		_secondaryButton.Text = "Позже";
	}

	private void OnDownloadFailed(Exception exception)
	{
		_downloading = false;

		_progressBar.Visible = false;
		_statusLabel.Text = $"Ошибка загрузки: {exception.Message}";
		_primaryButton.Text = "Загрузить";
		_primaryButton.Enabled = true;
		_secondaryButton.Text = "Позже";
	}

	private void Postpone()
	{
		_cancellation?.Cancel();
		Result = new UpdateDialogResult(UpdateDecision.Postpone, null);
		Application.RequestStop(this);
	}

	private static string BuildNotes(UpdateInfo info)
	{
		if (string.IsNullOrWhiteSpace(info.Notes))
			return "Описание изменений в релизе не указано.";

		string[] lines = info.Notes
			.Replace("\r\n", "\n")
			.Split('\n')
			.Take(NotesLineLimit)
			.ToArray();

		return string.Join("\n", lines).TrimEnd();
	}

	private static void RunOnUi(Action action) => Application.MainLoop?.Invoke(action);

	/// <summary>
	/// Прогресс без <see cref="Progress{T}"/>: тот доставляет колбэки через контекст синхронизации
	/// и может переставить проценты местами. Здесь вызов идёт прямо из потока загрузки, по порядку.
	/// </summary>
	private sealed class InlineProgress(Action<int> handler) : IProgress<int>
	{
		public void Report(int value) => handler(value);
	}
}
