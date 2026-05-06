	using Autofac;
using Terminal.Gui;
using YamBassPlayer.Enums;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Spectrum;

namespace YamBassPlayer.Views.Impl;

public sealed class MainWindow : Window
{
	private const string YamBassPlayerTitle = "YamBassPlayer";

	private readonly MainWindowCoordinator _coordinator;
	private readonly ITracksPresenter _tracksPresenter;
	private readonly IPlaybackQueue _playbackQueue;
	private readonly IAudioPlayer _audioPlayer;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly TracksViewHost _tracksView;
	private SplashScreenView? _splashScreen;
	private SpectrumView _spectrum = null!;
	private Button _spectrumModeButton = null!;
	private Button _spectrumFreqButton = null!;
	private TextField _filterField = null!;
	private MenuItem _tracksViewMenuItem = null!;
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
		: base(YamBassPlayerTitle)
	{
		_coordinator = coordinator;
		_tracksPresenter = tracksPresenter;
		_playbackQueue = playbackQueue;
		_audioPlayer = audioPlayer;
		_playlistsPresenter = playlistsPresenter;
		_tracksView = tracksView;

		// ── Menu bar (pure UI composition) ─────────────────────────────
		MenuBar menuBar = CreateMenuBar();
		Application.Top.Add(menuBar);

		// ── View layout ────────────────────────────────────────────────
		playStatusView.X = 0;
		playStatusView.Y = Pos.AnchorEnd(7);
		playStatusView.Width = Dim.Fill();
		playStatusView.Height = 5;

		commandInputView.X = 0;
		commandInputView.Y = Pos.AnchorEnd(2);
		commandInputView.Width = Dim.Fill();
		commandInputView.Height = 2;

		playlistsView.X = 0;
		playlistsView.Width = 30;
		playlistsView.Height = Dim.Fill(22);

		const int panelWidth = 38;

		_filterField = new TextField("")
		{
			X = Pos.Right(playlistsView),
			Y = 0,
			Width = Dim.Fill(panelWidth),
			Height = 1
		};
		_filterField.TextChanged += _ =>
		{
			var text = _filterField.Text?.ToString() ?? "";
			tracksView.SetFilter(string.IsNullOrWhiteSpace(text) ? null : text.Trim());
		};

		tracksView.X = Pos.Right(playlistsView);
		tracksView.Y = 1;
		tracksView.Width = Dim.Fill(panelWidth);
		tracksView.Height = Dim.Fill(8);

		trackInfoPanelView.X = Pos.Right(tracksView);
		trackInfoPanelView.Y = 0;
		trackInfoPanelView.Width = panelWidth;
		trackInfoPanelView.Height = Dim.Fill(7);

		_spectrum = new SpectrumView(bars: 29)
		{
			X = 0,
			Y = Pos.Top(playStatusView) - 15,
			Width = 29,
			Height = 14
		};
		_spectrum.AddRenderer(new BarsRenderer(29));
		_spectrum.AddRenderer(new OscilloscopeRenderer());
		_spectrum.AddRenderer(new PolarWaveformRenderer());
		_spectrum.AddRenderer(new LissajousScopeRenderer());
		_spectrum.AddRenderer(new WaterfallRenderer());
		_spectrum.AddRenderer(new RingsRenderer());
		_spectrum.AddRenderer(new Tunnel3DRenderer());
		_spectrum.AddRenderer(new StereoPanScopeRenderer());

		_spectrumModeButton = new Button
		{
			X = 0,
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

		Add(playlistsView, _spectrum, _spectrumModeButton, _spectrumFreqButton, _filterField, tracksView, trackInfoPanelView, playStatusView, commandInputView);

		// ── Wire presenter events via coordinator ──────────────────────
		_splashScreen = new SplashScreenView();
		Application.Top.Add(_splashScreen);
		_coordinator.SetWindow(this);
		_coordinator.WireEvents(_splashScreen);

		// Initialize playlists (fire-and-forget, was in PlaylistsPresenter constructor)
		_ = _playlistsPresenter.InitializeAsync();

		// Spectrum refresh timer (UI-only concern).
		// Skips while playback is idle and while a modal (e.g. «Сейчас играет») has its
		// own spectrum loop — otherwise two concurrent 16ms loops double the BASS polling
		// and full-view repaints per frame.
		Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			if (!ReferenceEquals(Application.Current, Application.Top) || !_audioPlayer.IsPlayed)
				return true;

			_spectrum.SetData(
				_spectrum.RequiredDataType == SpectrumDataType.Waveform
					? _audioPlayer.GetWaveformData(512)
					: _audioPlayer.ChannelGetData());
			return true;
		});
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
				new MenuItem("По умолчанию", "", () => Themes.RestoreDefaultTheme())
			}),
			new MenuBarItem("Аудио", new[]
			{
				new MenuItem("Эквалайзер", "", _coordinator.ShowEqualizer)
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
	}

	private void ToggleSpectrumMode()
	{
		_spectrum.CycleMode();
		_spectrumModeButton.Text = _spectrum.ModeDisplayName;
	}

	private void CycleSpectrumFreq()
	{
		_freqPresetIndex = (_freqPresetIndex + 1) % FreqPresets.Length;
		int freq = FreqPresets[_freqPresetIndex];
		_spectrum.MaxFrequencyHz = freq;
		_spectrumFreqButton.Text = freq >= 22050 ? "▲ 22k" : $"▲ {freq / 1000}k";
	}
}
