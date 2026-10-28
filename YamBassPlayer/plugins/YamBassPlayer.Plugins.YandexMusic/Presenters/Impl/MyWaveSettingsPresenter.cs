using Serilog;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

/// <summary>
/// Настройки «Моей волны»: окно открывается на UI-потоке, выбор запоминается
/// в настройках плагина, применяется к станции и только потом запускается волна.
/// </summary>
public sealed class MyWaveSettingsPresenter : IMyWaveSettingsPresenter
{
	private const string DiversityKey = "myWaveDiversity";
	private const string MoodEnergyKey = "myWaveMoodEnergy";
	private const string LanguageKey = "myWaveLanguage";

	private readonly IYandexRadioService _radio;
	private readonly IMyWaveService _myWave;
	private readonly IPluginUi _ui;
	private readonly IPluginSettings _settings;
	private readonly IErrorHandler _errorHandler;
	private readonly Func<IMyWaveSettingsView> _createView;

	public MyWaveSettingsPresenter(
		IYandexRadioService radio,
		IMyWaveService myWave,
		IPluginUi ui,
		IPluginSettings settings,
		IErrorHandler errorHandler,
		Func<IMyWaveSettingsView> createView)
	{
		_radio = radio;
		_myWave = myWave;
		_ui = ui;
		_settings = settings;
		_errorHandler = errorHandler;
		_createView = createView;
	}

	public async Task ShowAsync()
	{
		try
		{
			MyWaveSettings? selected = await ShowDialogAsync();
			if (selected is null)
				return;

			Save(selected);

			_ui.SetStatus("Применяем настройки «Моей волны»...");

			if (!await _radio.ApplyMyWaveSettingsAsync(selected))
				Log.Warning("Настройки «Моей волны» не применены — станция запустится с прежними");

			await _myWave.ShowAsync(_ => { });
		}
		catch (Exception ex)
		{
			_errorHandler.Handle(ex);
		}
	}

	/// <summary>
	/// Пункт меню плагина выполняется в фоновой задаче, а <c>Application.Run</c>
	/// обязан идти в UI-потоке: иначе драйвер Terminal.Gui крутится из двух потоков
	/// сразу и отрисовка падает. Поэтому окно создаётся и показывается через
	/// диспетчер, а выбор читается после закрытия.
	/// </summary>
	private Task<MyWaveSettings?> ShowDialogAsync()
	{
		var completion = new TaskCompletionSource<MyWaveSettings?>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		_ui.Dispatcher.Invoke(() =>
		{
			try
			{
				IMyWaveSettingsView view = _createView();
				view.SetSettings(Load());

				view.OnStartClicked += () =>
				{
					completion.TrySetResult(view.SelectedSettings);
					view.Close();
				};
				view.OnCancelClicked += () =>
				{
					completion.TrySetResult(null);
					view.Close();
				};

				view.Show();

				// Окно закрыли без «Запустить» (Esc или крестик) — это отмена.
				completion.TrySetResult(null);
			}
			catch (Exception ex)
			{
				completion.TrySetException(ex);
			}
		});

		return completion.Task;
	}

	private MyWaveSettings Load() => new(
		MyWaveSettingsOptions.Normalize(
			MyWaveSettingsOptions.Diversity, _settings.Get(DiversityKey), MyWaveSettingsOptions.DefaultDiversity),
		MyWaveSettingsOptions.Normalize(
			MyWaveSettingsOptions.MoodEnergy, _settings.Get(MoodEnergyKey), MyWaveSettingsOptions.DefaultMoodEnergy),
		MyWaveSettingsOptions.Normalize(
			MyWaveSettingsOptions.Language, _settings.Get(LanguageKey), MyWaveSettingsOptions.DefaultLanguage));

	private void Save(MyWaveSettings settings)
	{
		_settings.Set(DiversityKey, settings.Diversity);
		_settings.Set(MoodEnergyKey, settings.MoodEnergy);
		_settings.Set(LanguageKey, settings.Language);
	}
}
