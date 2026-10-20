using Autofac;
using Terminal.Gui;
using YamBassPlayer.Constants;
using YamBassPlayer.Enums;
using YamBassPlayer.Presenters;
using YamBassPlayer.Configuration;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Spectrum;

namespace YamBassPlayer.Views.Impl;

public sealed class MainWindow : Window
{
	/// <summary>Нижняя полоса со спектром: командная строка 2, панель 6, кнопки спектра 1, спектр 10.</summary>
	private const int BottomHeightWithSpectrum = 19;

	/// <summary>Без спектра остаются командная строка 2, панель 6 и ряд кнопок 1.</summary>
	private const int BottomHeightWithoutSpectrum = 9;

	/// <summary>Ширина панели «Инфо» справа от списка треков.</summary>
	private const int InfoPanelWidth = 38;

	private readonly MainWindowCoordinator _coordinator;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly TracksViewHost _tracksView;
	private readonly TrackInfoPanelView _trackInfoPanel;
	private SplashScreenView? _splashScreen;
	private SpectrumView _spectrum = null!;
	private Button _spectrumModeButton = null!;
	private Button _spectrumFreqButton = null!;
	private Button _spectrumToggleButton = null!;
	private TextField _filterField = null!;
	private InfoPanelToggleView _infoPanelToggle = null!;
	private MenuItem _tracksViewMenuItem = null!;
	private bool _spectrumEnabled = true;
	private bool _infoPanelVisible = true;
	private int _freqPresetIndex = 4;
	private static readonly int[] FreqPresets = [4000, 8000, 12000, 16000, 22050];

	public MainWindow(
		MainWindowCoordinator coordinator,
		IPlaylistsPresenter playlistsPresenter,
		ITracksPresenter tracksPresenter,
		IPlayStatusPresenter playStatusPresenter,
		ITrackInfoPanelPresenter trackInfoPanelPresenter,
		IPlaybackPresenter playbackCoordinator,
		IPlaybackQueue playbackQueue,
		IAudioPlayer audioPlayer,
		PlayStatusView playStatusView,
		CommandInputView commandInputView,
		CommandInputPresenter commandInputPresenter,
		PlaylistsView playlistsView,
		TracksViewHost tracksView,
		TrackInfoPanelView trackInfoPanelView)
		: base(AppInfo.WindowTitle)
	{
		_coordinator = coordinator;
		_tracksPresenter = tracksPresenter;
		_playbackQueue = playbackQueue;
		_audioPlayer = audioPlayer;
		_playlistsPresenter = playlistsPresenter;
		_tracksView = tracksView;
		_trackInfoPanel = trackInfoPanelView;

		// ── Menu bar (pure UI composition) ─────────────────────────────
		MenuBar menuBar = CreateMenuBar();
		Application.Top.Add(menuBar);

		// ── View layout ────────────────────────────────────────────────
		// Нижняя полоса (снизу вверх): командная строка 2, панель управления 6,
		// ряд кнопок спектра 1 и сам спектр 10 — итого 19 строк.
		const int bottomHeight = BottomHeightWithSpectrum;
		const int spectrumHeight = 10;
		const int playlistsWidth = 30;

		playStatusView.X = 0;
		playStatusView.Y = Pos.AnchorEnd(8);
		playStatusView.Width = Dim.Fill();
		playStatusView.Height = 6;

		commandInputView.X = 0;
		commandInputView.Y = Pos.AnchorEnd(2);
		commandInputView.Width = Dim.Fill();
		commandInputView.Height = 2;

		// Дерево плейлистов идёт до самой панели управления: спектр больше не
		// отрезает у него нижнюю часть, потому что начинается правее дерева.
		playlistsView.X = 0;
		playlistsView.Width = playlistsWidth;
		playlistsView.Height = Dim.Fill(7);

		_filterField = new TextField("")
		{
			X = Pos.Right(playlistsView),
			Y = 0,
			Width = Dim.Fill(InfoPanelWidth + InfoPanelToggleView.ToggleWidth),
			Height = 1
		};
		_filterField.TextChanged += _ =>
		{
			var text = _filterField.Text?.ToString() ?? "";
			tracksView.SetFilter(string.IsNullOrWhiteSpace(text) ? null : text.Trim());
		};

		tracksView.X = Pos.Right(playlistsView);
		tracksView.Y = 1;
		tracksView.Width = Dim.Fill(InfoPanelWidth);
		tracksView.Height = Dim.Fill(bottomHeight + 1);

		trackInfoPanelView.X = Pos.Right(tracksView);
		trackInfoPanelView.Y = 0;
		trackInfoPanelView.Width = InfoPanelWidth;
		trackInfoPanelView.Height = Dim.Fill(bottomHeight);

		// Кнопка сворачивания панели «Инфо» — над списком треков, вплотную к панели.
		// Она всегда на виду: в свёрнутом виде прижата к правому краю окна.
		_infoPanelToggle = new InfoPanelToggleView
		{
			X = Pos.AnchorEnd(InfoPanelWidth + InfoPanelToggleView.ToggleWidth),
			Y = 0
		};
		_infoPanelToggle.Toggled += ToggleInfoPanel;

		// Спектр — широкая полоса под списком треков и панелью инфо: полосам FFT
		// нужна ширина, а узкий угол внизу слева её не давал.
		_spectrum = new SpectrumView
		{
			X = playlistsWidth,
			Y = Pos.Top(playStatusView) - (spectrumHeight + 1),
			Width = Dim.Fill(playlistsWidth),
			Height = spectrumHeight
		};
		_spectrum.AddRenderer(new BarsRenderer());
		_spectrum.AddRenderer(new OscilloscopeRenderer());
		_spectrum.AddRenderer(new PolarWaveformRenderer());
		_spectrum.AddRenderer(new LissajousScopeRenderer());
		_spectrum.AddRenderer(new WaterfallRenderer());
		_spectrum.AddRenderer(new RingsRenderer());
		_spectrum.AddRenderer(new Tunnel3DRenderer());
		_spectrum.AddRenderer(new StereoPanScopeRenderer());

		_spectrumModeButton = new Button
		{
			X = playlistsWidth,
			Y = Pos.Top(playStatusView) - 1,
			Width = 14,
			Text = _spectrum.ModeDisplayName
		};
		_spectrumModeButton.Clicked += ToggleSpectrumMode;

		_spectrumFreqButton = new Button
		{
			X = Pos.Right(_spectrumModeButton),
			Y = Pos.Top(playStatusView) - 1,
			Width = 15,
			Text = "▲ 22k"
		};
		_spectrumFreqButton.Clicked += CycleSpectrumFreq;

		_spectrumToggleButton = new Button
		{
			X = Pos.Right(_spectrumFreqButton),
			Y = Pos.Top(playStatusView) - 1,
			Width = 14,
			Text = GetSpectrumToggleText(true)
		};
		_spectrumToggleButton.Clicked += ToggleSpectrumEnabled;

		RestoreSpectrumSettings();
		ApplyInfoPanelVisible(AppConfiguration.GetInfoPanelVisible());

		Add(playlistsView, _spectrum, _spectrumModeButton, _spectrumFreqButton, _spectrumToggleButton,
			_filterField, _infoPanelToggle, tracksView, trackInfoPanelView, playStatusView, commandInputView);

		// ── Wire presenter events via coordinator ──────────────────────
		_splashScreen = new SplashScreenView();
		Application.Top.Add(_splashScreen);
		_coordinator.SetWindow(this);
		_coordinator.WireEvents(_splashScreen);

		// Список треков — основной рабочий элемент: фокус должен стоять на активном
		// виде (таблица или плитки), иначе подсветка выбранной строки и клавиатура
		// работают не там, где ждёт пользователь. Сплеш снимается по первому
		// выбранному плейлисту — тогда возвращаем фокус ещё раз.
		Loaded += () => tracksView.FocusActive();
		_playlistsPresenter.PlaylistChosen += _ => tracksView.FocusActive();

		// Загрузка дерева плейлистов. Сплеш снимается и по первому выбранному
		// плейлисту, и по завершении загрузки: если плейлистов не оказалось,
		// выбора не будет, и заставка осталась бы висеть поверх интерфейса.
		_ = InitializePlaylistsAsync();

		// Spectrum refresh timer (UI-only concern).
		// Skips while playback is idle and while a modal (e.g. «Сейчас играет») has its
		// own spectrum loop — otherwise two concurrent 16ms loops double the BASS polling
		// and full-view repaints per frame.
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (!_spectrumEnabled || !ReferenceEquals(Application.Current, Application.Top) || !_audioPlayer.IsPlayed)
				return true;

			_spectrum.SetData(
				_spectrum.RequiredDataType == SpectrumDataType.Waveform
					? _audioPlayer.GetWaveformData(512)
					: _audioPlayer.ChannelGetData());
			return true;
		});
	}

	/// <summary>
	/// Грузит дерево плейлистов и снимает заставку в любом случае — даже если
	/// плейлистов не оказалось и выбирать нечего. Иначе сплеш остался бы поверх
	/// интерфейса и приложение было бы заблокировано.
	/// </summary>
	private async Task InitializePlaylistsAsync()
	{
		try
		{
			await _playlistsPresenter.InitializeAsync();
		}
		finally
		{
			Application.MainLoop.Invoke(RemoveSplashScreen);
		}
	}

	private void RemoveSplashScreen()
	{
		if (_splashScreen?.SuperView is not null)
			Application.Top.Remove(_splashScreen);
	}

	private MenuBar CreateMenuBar()
	{
		_tracksViewMenuItem = new MenuItem(null, null, ToggleTracksViewMode)
		{
			Title = _tracksView.IsTilesActive ? "Вид треков: Плитки" : "Вид треков: Таблица"
		};

		return new MenuBar(new[]
		{
			new MenuBarItem("Файл", new[]
			{
				new MenuItem("Проверка обновлений…", "[Проверить наличие новой версии]", _coordinator.ShowUpdateDialog),
				new MenuItem("О программе", "[Сведения о программе]", _coordinator.ShowAbout),
				null,
				new MenuItem("Выход", "[Выход из программы]", _coordinator.StopApplication)
			}),
			new MenuBarItem("Темы", new[]
			{
				new MenuItem("Тёмная", "", () => Themes.ApplyDarkTheme()),
				new MenuItem("Светлая", "", () => Themes.ApplyLightTheme()),
				new MenuItem("Белая", "", () => Themes.ApplyWhiteTheme()),
				new MenuItem("Матрица", "", () => Themes.ApplyMatrixTheme()),
				new MenuItem("Киберпанк", "", () => Themes.ApplyCyberpunkTheme()),
				new MenuItem("Спокойная", "", () => Themes.ApplyNordTheme()),
				new MenuItem("Янтарь", "", () => Themes.ApplyAmberTheme()),
				new MenuItem("Мятная", "", () => Themes.ApplyMintTheme()),
				new MenuItem("По умолчанию", "", () => Themes.RestoreDefaultTheme())
			}),
			new MenuBarItem("Аудио", new[]
			{
				new MenuItem("Эквалайзер", "", _coordinator.ShowEqualizer),
				null,
				new MenuItem("Рекомендовать следующий", "", _coordinator.RecommendNextTrack)
			}),
			new MenuBarItem("Инструменты", new[]
			{
				new MenuItem("Локальный поиск", "", _coordinator.ShowLocalSearchDialog),
				new MenuItem("Поиск по ЯМ", "", _coordinator.ShowYandexSearchDialog),
				new MenuItem("Моя волна [F9]", "", _coordinator.ShowMyWave),
				new MenuItem("Моя волна по треку", "", () => _coordinator.ShowMyWaveByTrack()),
				new MenuItem("Статистика БД", "", _coordinator.ShowDbStats),
				null,
				new MenuItem("Добавить папку...", "", _coordinator.ShowAddLocalFolderDialog),
				new MenuItem("Управление папками...", "", _coordinator.ShowLocalFolderManagerDialog),
				new MenuItem("Сканировать библиотеку", "", _coordinator.ScanLocalLibrary)
			}),
			new MenuBarItem("Вид", new[]
			{
				new MenuItem("Визуализация [F5]", "", _coordinator.ShowNowPlaying),
				new MenuItem("Крупное инфо [F8]", "", _coordinator.ShowLargeTrackInfo),
				new MenuItem("Скрыть/показать панель «Инфо»", "", ToggleInfoPanel),
				null,
				new MenuItem("≋ Переключить режим спектра", "", ToggleSpectrumMode),
				null,
				_tracksViewMenuItem
			})
		});
	}

	private void ToggleTracksViewMode()
	{
		_tracksView.ToggleView();
		_tracksViewMenuItem.Title = _tracksView.IsTilesActive
			? "Вид треков: Плитки"
			: "Вид треков: Таблица";
		Logging.LogUserAction($"вид треков: {(_tracksView.IsTilesActive ? "плитки" : "таблица")}");
	}

	private void RestoreSpectrumSettings()
	{
		int savedMode = AppConfiguration.GetSpectrumMode();
		if (savedMode >= 0)
			_spectrum.SelectMode(savedMode);

		int savedFreq = AppConfiguration.GetSpectrumFrequency();
		if (savedFreq > 0)
		{
			_freqPresetIndex = Math.Max(0, Array.IndexOf(FreqPresets, savedFreq));
			_spectrum.MaxFrequencyHz = savedFreq;
		}

		_spectrumModeButton.Text = _spectrum.ModeDisplayName;
		_spectrumFreqButton.Text = _spectrum.MaxFrequencyHz >= 22050
			? "▲ 22k"
			: $"▲ {_spectrum.MaxFrequencyHz / 1000}k";

		ApplySpectrumEnabled(AppConfiguration.GetSpectrumEnabled());
	}

	/// <summary>
	/// Показывает или скрывает панель «Инфо». Скрытая панель отдаёт свои колонки
	/// списку треков и полю фильтра, а кнопка-стрелка остаётся на виду — вплотную
	/// к панели или у правого края окна, — чтобы панель можно было вернуть.
	/// </summary>
	private void ApplyInfoPanelVisible(bool visible)
	{
		_infoPanelVisible = visible;
		_trackInfoPanel.Visible = visible;
		_infoPanelToggle.Expanded = visible;

		int reserved = visible
			? InfoPanelWidth + InfoPanelToggleView.ToggleWidth
			: InfoPanelToggleView.ToggleWidth;

		_infoPanelToggle.X = Pos.AnchorEnd(reserved);
		_filterField.Width = Dim.Fill(reserved);
		_tracksView.Width = visible ? Dim.Fill(InfoPanelWidth) : Dim.Fill();

		SetNeedsDisplay();
	}

	private void ToggleInfoPanel()
	{
		ApplyInfoPanelVisible(!_infoPanelVisible);
		AppConfiguration.SaveInfoPanelVisible(_infoPanelVisible);
		Logging.LogUserAction(_infoPanelVisible ? "панель «Инфо» показана" : "панель «Инфо» скрыта");
	}

	/// <summary>
	/// Включает/выключает визуализацию: скрывает сам спектр и его кнопки режима/частоты,
	/// а освободившиеся строки отдаёт списку треков и панели информации.
	/// </summary>
	private void ApplySpectrumEnabled(bool enabled)
	{
		_spectrumEnabled = enabled;
		_spectrum.Visible = enabled;
		_spectrumModeButton.Visible = enabled;
		_spectrumFreqButton.Visible = enabled;
		_spectrumToggleButton.Text = GetSpectrumToggleText(enabled);

		int bottomHeight = enabled ? BottomHeightWithSpectrum : BottomHeightWithoutSpectrum;
		_tracksView.Height = Dim.Fill(bottomHeight + 1);
		_trackInfoPanel.Height = Dim.Fill(bottomHeight);
		SetNeedsDisplay();
	}

	private void ToggleSpectrumEnabled()
	{
		ApplySpectrumEnabled(!_spectrumEnabled);
		AppConfiguration.SaveSpectrumEnabled(_spectrumEnabled);
		Logging.LogUserAction($"визуализация: {(_spectrumEnabled ? "включена" : "выключена")}");
	}

	private static string GetSpectrumToggleText(bool enabled)
		=> enabled ? "Спектр: вкл" : "Спектр: выкл";

	private void ToggleSpectrumMode()
	{
		_spectrum.CycleMode();
		_spectrumModeButton.Text = _spectrum.ModeDisplayName;
		AppConfiguration.SaveSpectrumMode(_spectrum.CurrentModeIndex);
		Logging.LogUserAction($"режим визуализации: {_spectrum.ModeDisplayName}");
	}

	private void CycleSpectrumFreq()
	{
		_freqPresetIndex = (_freqPresetIndex + 1) % FreqPresets.Length;
		int freq = FreqPresets[_freqPresetIndex];
		_spectrum.MaxFrequencyHz = freq;
		_spectrumFreqButton.Text = freq >= 22050 ? "▲ 22k" : $"▲ {freq / 1000}k";
		AppConfiguration.SaveSpectrumFrequency(freq);
		Logging.LogUserAction($"частота визуализации: {freq} Гц");
	}
}
