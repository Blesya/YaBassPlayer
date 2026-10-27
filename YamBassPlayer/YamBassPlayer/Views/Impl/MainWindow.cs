using Autofac;
using Terminal.Gui;
using YamBassPlayer.Constants;
using YamBassPlayer.Enums;
using YamBassPlayer.Presenters;
using YamBassPlayer.Configuration;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Plugins;
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

	/// <summary>Ширина развёрнутого дерева плейлистов.</summary>
	private const int PlaylistsWidth = 30;

	/// <summary>Высота панели «Управление воспроизведением».</summary>
	private const int PlayStatusHeight = 6;

	/// <summary>Высота командной строки внизу окна.</summary>
	private const int CommandInputHeight = 2;

	/// <summary>Период обновления визуализации, мс (≈60 кадров в секунду).</summary>
	private const int SpectrumRefreshMs = 16;

	/// <summary>
	/// Сколько строк занимает низ окна: панель управления и командная строка.
	/// От этой величины отсчитывается и верх панели, и низ дерева плейлистов —
	/// иначе рамки двух вьюх попадают на одну строку и нижняя рамка дерева
	/// прячется под панелью.
	/// </summary>
	private const int ControlPanelFromBottom = PlayStatusHeight + CommandInputHeight;

	private readonly MainWindowCoordinator _coordinator;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly TracksViewHost _tracksView;
	private readonly TrackInfoPanelView _trackInfoPanel;
	private readonly PlaylistsView _playlistsView;
	private readonly MenuRegistry _menuRegistry;
	private readonly PluginLoadReportStore _pluginReports;
	private MenuBarItem _pluginsMenuBarItem = null!;
	private SplashScreenView? _splashScreen;
	private SpectrumView _spectrum = null!;
	private Button _spectrumModeButton = null!;
	private Button _spectrumFreqButton = null!;
	private Button _spectrumToggleButton = null!;
	private TextField _filterField = null!;
	private PanelToggleView _infoPanelToggle = null!;
	private PanelToggleView _playlistsToggle = null!;
	private MenuItem _tracksViewMenuItem = null!;
	private bool _spectrumEnabled = true;
	private bool _infoPanelVisible = true;
	private bool _playlistsCollapsed;
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
		TrackInfoPanelView trackInfoPanelView,
		MenuRegistry menuRegistry,
		PluginLoadReportStore pluginReports)
		: base(AppInfo.WindowTitle)
	{
		_coordinator = coordinator;
		_tracksPresenter = tracksPresenter;
		_playbackQueue = playbackQueue;
		_audioPlayer = audioPlayer;
		_playlistsPresenter = playlistsPresenter;
		_tracksView = tracksView;
		_trackInfoPanel = trackInfoPanelView;
		_playlistsView = playlistsView;
		_menuRegistry = menuRegistry;
		_pluginReports = pluginReports;

		// ── Menu bar (pure UI composition) ─────────────────────────────
		MenuBar menuBar = CreateMenuBar();
		Application.Top.Add(menuBar);

		foreach (PluginMenuGroup group in _menuRegistry.Groups)
			group.Plugin.Changed += RefreshPluginMenu;

		// ── View layout ────────────────────────────────────────────────
		// Нижняя полоса (снизу вверх): командная строка 2, панель управления 6,
		// ряд кнопок спектра 1 и сам спектр 10 — итого 19 строк.
		const int bottomHeight = BottomHeightWithSpectrum;
		const int spectrumHeight = 10;

		playStatusView.X = 0;
		playStatusView.Y = Pos.AnchorEnd(ControlPanelFromBottom);
		playStatusView.Width = Dim.Fill();
		playStatusView.Height = PlayStatusHeight;

		commandInputView.X = 0;
		commandInputView.Y = Pos.AnchorEnd(CommandInputHeight);
		commandInputView.Width = Dim.Fill();
		commandInputView.Height = CommandInputHeight;

		// Дерево плейлистов идёт до самой панели управления: спектр больше не
		// отрезает у него нижнюю часть, потому что начинается правее дерева.
		// Высота считается от того же смещения, что и верх панели управления,
		// поэтому нижняя рамка дерева ложится строкой выше и не перекрывается.
		// Свёрнутая панель уезжает влево целиком: ширину ей выставляет
		// ApplyPlaylistsCollapsed, а соседи отсчитываются от её правого края.
		playlistsView.X = 0;
		playlistsView.Width = PlaylistsWidth;
		playlistsView.Height = Dim.Fill(ControlPanelFromBottom);

		// Кнопка сворачивания дерева — справа от панели, на строке фильтра:
		// так же, как кнопка панели «Инфо» стоит слева от своей панели.
		// В свёрнутом виде она прижимается к левому краю окна и остаётся на виду.
		_playlistsToggle = new PanelToggleView
		{
			X = Pos.Right(playlistsView),
			Y = 0,
			Side = PanelSide.Left
		};
		_playlistsToggle.Toggled += TogglePlaylists;

		_filterField = new TextField("")
		{
			X = Pos.Right(playlistsView) + PanelToggleView.ToggleWidth,
			Y = 0,
			Width = Dim.Fill(InfoPanelWidth + PanelToggleView.ToggleWidth),
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

		// Панель «Инфо» тянется вниз до панели управления, как и дерево плейлистов:
		// внизу у неё текст песни, и каждая отданная строка — видимая строчка.
		trackInfoPanelView.X = Pos.Right(tracksView);
		trackInfoPanelView.Y = 0;
		trackInfoPanelView.Width = InfoPanelWidth;
		trackInfoPanelView.Height = Dim.Fill(ControlPanelFromBottom);

		// Кнопка сворачивания панели «Инфо» — над списком треков, вплотную к панели.
		// Она всегда на виду: в свёрнутом виде прижата к правому краю окна.
		_infoPanelToggle = new PanelToggleView
		{
			X = Pos.AnchorEnd(InfoPanelWidth + PanelToggleView.ToggleWidth),
			Y = 0,
			Side = PanelSide.Right
		};
		_infoPanelToggle.Toggled += ToggleInfoPanel;

		// Спектр — полоса под списком треков и ровно его ширины: полосам FFT
		// нужно место, но правый столбец занят панелью «Инфо», и заходить
		// под неё спектр не должен. Левый край идёт за деревом плейлистов:
		// у свёрнутой панели он сдвигается вместе с ним.
		_spectrum = new SpectrumView
		{
			X = Pos.Right(playlistsView),
			Y = Pos.Top(playStatusView) - (spectrumHeight + 1),
			Width = Dim.Fill(InfoPanelWidth),
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
			X = Pos.Right(playlistsView),
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
		ApplyPlaylistsCollapsed(AppConfiguration.GetPlaylistsCollapsed());

		Add(playlistsView, _playlistsToggle, _spectrum, _spectrumModeButton, _spectrumFreqButton,
			_spectrumToggleButton, _filterField, _infoPanelToggle, tracksView, trackInfoPanelView,
			playStatusView, commandInputView);

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
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(SpectrumRefreshMs), _ =>
		{
			if (!_spectrumEnabled || !ReferenceEquals(Application.Current, Application.Top) || !_audioPlayer.IsPlayed)
				return true;

			bool drawn = _spectrum.DrawFrame(
				_spectrum.RequiredDataType == SpectrumDataType.Waveform
					? _audioPlayer.GetWaveformData(512)
					: _audioPlayer.ChannelGetData());

			// Спектр рисует себя сам, без инвалидации Terminal.Gui: иначе каждая
			// инвалидация заставляет Toplevel залить весь экран и перерисовать все
			// панели (включая обложку) — 60 раз в секунду.
			if (drawn)
			{
				Application.Top.PositionCursor();
				Application.Driver.Refresh();
			}

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

		_pluginsMenuBarItem = new MenuBarItem("Плагины", BuildPluginMenuItems());

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
				new MenuItem("Статистика БД", "", _coordinator.ShowDbStats)
			}),
			_pluginsMenuBarItem,
			new MenuBarItem("Вид", new[]
			{
				new MenuItem("Визуализация [F5]", "", _coordinator.ShowNowPlaying),
				new MenuItem("О треке [F7]", "", _coordinator.ShowTrackAbout),
				new MenuItem("Крупное инфо [F8]", "", _coordinator.ShowLargeTrackInfo),
				new MenuItem("Скрыть/показать панель «Инфо»", "", ToggleInfoPanel),
				new MenuItem("Свернуть/развернуть панель «Плейлисты»", "", TogglePlaylists),
				null,
				new MenuItem("≋ Переключить режим спектра", "", ToggleSpectrumMode),
				null,
				_tracksViewMenuItem
			})
		});
	}

	/// <summary>
	/// Плагин изменил состояние (вход, папки) — пересобираем подменю,
	/// чтобы подписи и доступность пунктов не отставали.
	/// </summary>
	private void RefreshPluginMenu()
	{
		Application.MainLoop.Invoke(() =>
		{
			_pluginsMenuBarItem.Children = BuildPluginMenuItems();
			Application.Top.SetNeedsDisplay();
		});
	}

	private MenuItem[] BuildPluginMenuItems()
	{
		var items = new List<MenuItem>();

		foreach (PluginMenuGroup group in _menuRegistry.Groups)
			items.Add(new MenuBarItem(group.DisplayName, BuildPluginEntries(group)));

		// Сломанный плагин остаётся видимым, но недоступным: иначе пропажа
		// пункта выглядит как «ничего не было», а причина — только в диагностике.
		foreach (PluginLoadReport report in _pluginReports.Reports.Where(report => !report.IsLoaded))
			items.Add(new MenuItem($"{report.Name} (ошибка)", "", () => ShowPluginDiagnostics(), () => false));

		if (items.Count > 0)
			items.Add(null!);

		items.Add(new MenuItem("Диагностика…", "", ShowPluginDiagnostics));
		return items.ToArray();
	}

	/// <summary>
	/// Пункты плагина — вложенное подменю. MenuBar в Terminal.Gui 1.19 раскрывает
	/// дочерний <see cref="MenuBarItem"/> как подменю, поэтому отдельное окно не нужно.
	/// </summary>
	private MenuItem[] BuildPluginEntries(PluginMenuGroup group)
	{
		var entries = new List<MenuItem>();

		foreach (PluginMenuEntry entry in group.Menu.GetEntries())
		{
			PluginMenuEntry captured = entry;
			entries.Add(new MenuItem(
				captured.Title,
				captured.Shortcut ?? "",
				() => RunPluginEntry(group, captured),
				() => captured.IsEnabled));
		}

		entries.Add(null!);
		entries.Add(new MenuItem("Настройка", "", () => ConfigurePlugin(group)));

		return entries.ToArray();
	}

	private void RunPluginEntry(PluginMenuGroup group, PluginMenuEntry entry)
	{
		Logging.LogUserAction($"плагин {group.PluginId}: {entry.Title}");

		_ = Task.Run(async () =>
		{
			try
			{
				await entry.Invoke();
			}
			catch (Exception ex)
			{
				Serilog.Log.Error(ex, "Действие плагина {PluginId} завершилось ошибкой", group.PluginId);
			}
		});
	}

	private void ConfigurePlugin(PluginMenuGroup group)
	{
		Logging.LogUserAction($"настройка плагина {group.PluginId}");
		_ = ConfigurePluginAsync(group);
	}

	private async Task ConfigurePluginAsync(PluginMenuGroup group)
	{
		try
		{
			await group.Plugin.ConfigureAsync(CancellationToken.None);
		}
		catch (Exception ex)
		{
			Serilog.Log.Error(ex, "Настройка плагина {PluginId} завершилась ошибкой", group.PluginId);
			Application.MainLoop.Invoke(() =>
				MessageBox.Query("Плагин", $"Не удалось настроить {group.DisplayName}:\n{ex.Message}", "OK"));
		}
	}

	private void ShowPluginDiagnostics()
	{
		IReadOnlyList<PluginLoadReport> reports = _pluginReports.Reports;
		if (reports.Count == 0)
		{
			MessageBox.Query("Плагины", "Плагины не загружены", "OK");
			return;
		}

		string text = string.Join("\n\n", reports.Select(DescribePlugin));

		MessageBox.Query("Плагины", text, "OK");
	}

	private static string DescribePlugin(PluginLoadReport report)
	{
		var lines = new List<string>
		{
			$"{report.Name} ({report.PluginId}) — {DescribePluginStatus(report)}",
			$"  путь: {report.Path}",
			$"  версия: {report.Version ?? "—"}",
			$"  контракт: {report.ContractVersion ?? "—"}"
		};

		if (!string.IsNullOrWhiteSpace(report.Error))
			lines.Add($"  ошибка: {report.Error}");

		return string.Join("\n", lines);
	}

	private static string DescribePluginStatus(PluginLoadReport report) => report.Status switch
	{
		PluginLoadStatus.Loaded => "загружен",
		PluginLoadStatus.Incompatible => "несовместим: " + report.Error,
		_ => "ошибка: " + report.Error
	};

	private void ToggleTracksViewMode()	{
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
			? InfoPanelWidth + PanelToggleView.ToggleWidth
			: PanelToggleView.ToggleWidth;

		_infoPanelToggle.X = Pos.AnchorEnd(reserved);
		_filterField.Width = Dim.Fill(reserved);
		_tracksView.Width = visible ? Dim.Fill(InfoPanelWidth) : Dim.Fill();
		_spectrum.Width = visible ? Dim.Fill(InfoPanelWidth) : Dim.Fill();

		SetNeedsDisplay();
	}

	private void ToggleInfoPanel()
	{
		ApplyInfoPanelVisible(!_infoPanelVisible);
		AppConfiguration.SaveInfoPanelVisible(_infoPanelVisible);
		Logging.LogUserAction(_infoPanelVisible ? "панель «Инфо» показана" : "панель «Инфо» скрыта");
	}

	/// <summary>
	/// Сворачивает или разворачивает дерево плейлистов. Свёрнутая панель уезжает
	/// влево целиком, а освободившиеся колонки забирают список треков, поле фильтра,
	/// спектр и его кнопки: все они отсчитываются от правого края панели, поэтому
	/// достаточно обнулить её ширину. Кнопка при этом прижимается к левому краю окна
	/// и остаётся на виду — как у панели «Инфо» в свёрнутом виде.
	/// </summary>
	private void ApplyPlaylistsCollapsed(bool collapsed)
	{
		_playlistsCollapsed = collapsed;
		_playlistsView.Visible = !collapsed;
		_playlistsView.Width = collapsed ? 0 : PlaylistsWidth;
		_playlistsToggle.Expanded = !collapsed;

		SetNeedsDisplay();
	}

	private void TogglePlaylists()
	{
		ApplyPlaylistsCollapsed(!_playlistsCollapsed);

		// Фокус не должен остаться на скрытом дереве: иначе клавиши «перестают
		// работать». Возвращаем его туда, где пользователь и ждёт, — в список треков.
		if (_playlistsCollapsed)
			_tracksView.FocusActive();

		AppConfiguration.SavePlaylistsCollapsed(_playlistsCollapsed);
		Logging.LogUserAction(_playlistsCollapsed ? "панель «Плейлисты» свёрнута" : "панель «Плейлисты» развёрнута");
	}

	/// <summary>
	/// Включает/выключает визуализацию: скрывает сам спектр и его кнопки режима/частоты,
	/// а освободившиеся строки отдаёт списку треков. Панель «Инфо» тянется до панели
	/// управления независимо от спектра, поэтому её высота здесь не меняется.
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
		_trackInfoPanel.Height = Dim.Fill(ControlPanelFromBottom);
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
