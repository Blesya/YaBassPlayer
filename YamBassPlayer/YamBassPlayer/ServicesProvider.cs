using Autofac;
using YamBassPlayer.Commands;
using YamBassPlayer.Configuration;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.UseCases;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer;

public static class ServicesProvider
{
	private static readonly string TracksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracks");
	private static readonly string CoversFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "covers");
		
	public static IContainer Ioc { get; private set; } = null!;

	public static void Initialise()
	{
		AppConfiguration.MigrateLegacyYandexSettings();

		var builder = new ContainerBuilder();

		// Регистрация внешних зависимостей
		builder.RegisterType<AppConfigurationProvider>().As<IAppConfiguration>().SingleInstance();
		builder.RegisterInstance(new PluginStateFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugin_state"))).As<IPluginStateFile>().SingleInstance();

		// Регистрация сервисов
		builder.RegisterType<AudioPlayerService>().As<IAudioPlayer>().SingleInstance();
		builder.RegisterType<GitHubReleaseFeed>().As<IReleaseFeed>().SingleInstance();
		builder.RegisterType<UpdateService>().As<IUpdateService>().SingleInstance();
		builder.RegisterType<UpdateInstaller>().As<IUpdateInstaller>().SingleInstance();
		builder.RegisterType<UpdateDialogView>().As<IUpdateDialogView>().SingleInstance();
		builder.RegisterType<UpdatePresenter>().As<IUpdatePresenter>().SingleInstance();
		builder.RegisterType<UpdateScheduler>().As<IUpdateScheduler>().SingleInstance();
		builder.RegisterType<BassEqualizer>().As<IBassEqualizer>().SingleInstance();
		builder.RegisterType<CoverArtService>().As<ICoverArtService>().SingleInstance();
		builder.RegisterType<SqliteConnectionFactory>().As<IDbConnectionFactory>().SingleInstance();
		builder.RegisterType<DatabaseInitializer>().As<IDatabaseInitializer>().SingleInstance();
		builder.RegisterType<DbWriteLock>().As<IDbWriteLock>().SingleInstance();
		builder.RegisterType<UiErrorHandler>().As<IErrorHandler>().SingleInstance();
		builder.RegisterType<UiDispatcher>().As<IUiDispatcher>().SingleInstance();
		builder.RegisterType<EventBus>().As<IEventBus>().SingleInstance();
			
		builder.RegisterType<HistoryService>().As<IHistoryService>().SingleInstance();
		builder.RegisterType<LocalLibraryRepository>().AsSelf().SingleInstance();
		builder.Register(c => new LocalLibraryScanner(
			c.Resolve<IDbConnectionFactory>(),
			CoversFolder,
			c.Resolve<LocalLibraryRepository>()
		)).AsSelf().SingleInstance();
		builder.Register(c => new LocalLibraryService(
			c.Resolve<LocalLibraryRepository>(),
			c.Resolve<LocalLibraryScanner>(),
			c.Resolve<IDbWriteLock>()
		)).As<ILocalLibraryService>().SingleInstance();
		builder.RegisterType<TrackRepositoryCache>().As<ITrackRepositoryCache>().SingleInstance();
		builder.Register(c => new PlaylistStateStore(
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlist_state.json"),
			c.Resolve<IPluginStateFile>()
		)).As<IPlaylistStateStore>().SingleInstance();
		builder.RegisterType<PlaybackPresenter>().As<IPlaybackPresenter>().SingleInstance();
		builder.RegisterType<LocalFavoriteService>()
			.As<ILocalFavoriteService>()
			.SingleInstance();
		builder.RegisterType<ListenTimer>().As<IListenTimer>().SingleInstance();
		builder.RegisterType<PlaybackQueue>().As<IPlaybackQueue>().SingleInstance();
			
		builder.RegisterType<TrackDownloadTracker>().As<ITrackDownloadTracker>().SingleInstance();

		builder.Register(c => new TrackFileProvider(
			TracksFolder,
			c.Resolve<ITrackDownloadTracker>(),
			c.Resolve<IMusicSourceRegistry>()
		)).As<ITrackFileProvider>().SingleInstance();

		builder.Register(c => new CoverProvider(
			CoversFolder,
			c.Resolve<IDbConnectionFactory>(),
			c.Resolve<IMusicSourceRegistry>()
		)).As<ICoverProvider>().SingleInstance();
			
		builder.RegisterType<TrackInfoProvider>().As<ITrackInfoProvider>().SingleInstance();
		builder.RegisterType<TrackCatalog>().As<ITrackCatalog>().SingleInstance();
		builder.RegisterType<TrackAboutService>().As<ITrackAboutService>().SingleInstance();
		builder.Register(c => new LyricsService(
			c.Resolve<IMusicSourceRegistry>(),
			c.Resolve<IDbConnectionFactory>(),
			c.Resolve<IDbWriteLock>()
		)).As<ILyricsService>().SingleInstance();
		builder.RegisterType<SourcesBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<TopByDayBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<GlobalArtistsBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<PlaylistTreeComposer>().As<IPlaylistTreeComposer>().SingleInstance();

		var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		builder.Register(c => new NextTrackPredictor(
			Path.Combine(baseDirectory, "next_model.zip"),
			Path.Combine(baseDirectory, "next_key_map.txt")
		)).As<INextTrackPredictor>().SingleInstance();
		
		builder.Register(c => new DatabaseStatisticsService(
			c.Resolve<IDbConnectionFactory>(),
			TracksFolder
		)).As<IDatabaseStatisticsService>().SingleInstance();

		// Playlist load strategies
		builder.RegisterType<Top10LoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<TopEveningsLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<TopByDayLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.Register(c => new CachedLoadStrategy(TracksFolder)).As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<ArtistLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<QueueLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<FavoriteLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<PlaylistLoadStrategyResolver>().AsSelf().SingleInstance();

		builder.Register(c => new AppPlaylistProvider(TracksFolder, c.Resolve<ILocalFavoriteService>())).As<IAppPlaylistProvider>().SingleInstance();

		builder.Register(c => new TrackRepository(
			c.Resolve<IMusicSourceRegistry>(),
			c.Resolve<ITrackInfoProvider>(),
			c.Resolve<IHistoryService>(),
			c.Resolve<ITrackRepositoryCache>(),
			c.Resolve<ILocalLibraryService>(),
			c.Resolve<PlaylistLoadStrategyResolver>(),
			c.Resolve<IAppPlaylistProvider>(),
			c.Resolve<IPlaylistStateStore>()
		)).As<ITrackRepository>().SingleInstance();

		// Регистрация источников музыки
		builder.RegisterType<MusicSourceRegistry>().AsSelf().As<IMusicSourceRegistry>().SingleInstance();

		builder.RegisterType<LocalMusicSource>().AsSelf().SingleInstance();

		// Плагины и их вклады
		builder.RegisterType<MenuRegistry>().AsSelf().SingleInstance();
		builder.Register(c =>
		{
			var registry = new TrackActionRegistry();
			registry.Add(new FavoriteTrackAction(c.Resolve<ILocalFavoriteService>()));
			return registry;
		}).AsSelf().SingleInstance();
		builder.RegisterType<PluginLoadReportStore>().AsSelf().SingleInstance();
		builder.RegisterType<PluginUi>().As<IPluginUi>().SingleInstance();
		builder.RegisterType<LocalMusicPlugin>().As<IPlugin>().SingleInstance();
		builder.Register(c => new PluginHostServices(
				c.Resolve<IErrorHandler>(),
				c.Resolve<IMyWaveService>(),
				c.Resolve<ITrackInfoProvider>(),
				c.Resolve<IPlaybackQueue>(),
				TracksFolder,
				CoversFolder))
			.As<IPluginHostServices>()
			.SingleInstance();
		builder.Register(c =>
		{
			// Ссылки на контекст резолва хранить нельзя: фабрика плагинов вызывается
			// позже, когда операция резолва уже завершена. Поэтому забираем готовые
			// экземпляры сразу, а в замыкании остаются только они.
			var plugins = c.Resolve<IEnumerable<IPlugin>>();
			var reports = c.Resolve<PluginLoadReportStore>();
			var sources = c.Resolve<MusicSourceRegistry>();
			var menus = c.Resolve<MenuRegistry>();
			var commands = c.Resolve<CommandRegistry>();
			var actions = c.Resolve<TrackActionRegistry>();
			var events = c.Resolve<IEventBus>();
			var stateFile = c.Resolve<IPluginStateFile>();
			var ui = c.Resolve<IPluginUi>();
			var host = c.Resolve<IPluginHostServices>();

			return new PluginLoader(
				plugins,
				reports,
				plugin => new PluginRegistrar(
					plugin,
					sources,
					menus,
					commands,
					actions,
					events,
					stateFile,
					ui,
					host));
		}).AsSelf().SingleInstance();

		// Фабрика вьюх (убирает Ioc.Resolve из презентеров)
		builder.RegisterType<AutofacViewFactory>().As<IViewFactory>().SingleInstance();

		// Хост модальных окон (одолживает PlayStatusView на время показа)
		builder.RegisterType<ModalWindowHost>().As<IModalWindowHost>().SingleInstance();

		// Регистрация Views
		builder.RegisterType<EqualizerView>().As<IEqualizerView>();
		builder.RegisterType<DatabaseStatisticsView>().As<IDatabaseStatisticsView>();
		builder.RegisterType<PlayStatusView>().As<IPlayStatusView>().AsSelf().SingleInstance();
		builder.RegisterType<CommandInputView>().As<ICommandInputView>().AsSelf().SingleInstance();
		builder.RegisterType<PlaylistsView>().As<IPlaylistsView>().AsSelf().SingleInstance();
		// Общая модель списка треков: таблица, плитки и хост получают один и тот же экземпляр.
		builder.RegisterType<TrackListModel>().As<ITrackListModel>().SingleInstance();
		builder.RegisterType<TracksTileView>().AsSelf().SingleInstance();
		builder.RegisterType<TracksView>().AsSelf().SingleInstance();
		builder.RegisterType<TracksViewHost>().As<ITracksView>().AsSelf().SingleInstance();
		builder.RegisterType<TrackInfoPanelView>().As<ITrackInfoPanelView>().AsSelf().SingleInstance();
		builder.RegisterType<LocalSearchView>().As<ILocalSearchView>();
		builder.RegisterType<LargeTrackInfoView>().As<ILargeTrackInfoView>();
		builder.RegisterType<TrackAboutView>().As<ITrackAboutView>();
		builder.RegisterType<LocalFolderManagerView>().As<ILocalFolderManagerView>().SingleInstance();
		builder.RegisterType<NowPlayingView>().As<INowPlayingView>();
		builder.RegisterType<MyWaveView>().As<IMyWaveView>();

		// Регистрация Presenters
		builder.RegisterType<PlayStatusPresenter>().As<IPlayStatusPresenter>().SingleInstance();
		builder.RegisterType<CommandInputPresenter>().AsSelf().SingleInstance();
		builder.RegisterType<PlaylistsPresenter>().As<IPlaylistsPresenter>().SingleInstance();
		builder.RegisterType<TracksPresenter>().As<ITracksPresenter>().SingleInstance();
		builder.RegisterType<TrackInfoPanelPresenter>().As<ITrackInfoPanelPresenter>().SingleInstance();
		builder.RegisterType<EqualizerPresenter>().As<IEqualizerPresenter>().SingleInstance();
		builder.RegisterType<LocalSearchPresenter>().As<ILocalSearchPresenter>().SingleInstance();
		builder.RegisterType<DatabaseStatisticsPresenter>().As<IDatabaseStatisticsPresenter>().SingleInstance();
		builder.RegisterType<NowPlayingPresenter>().As<INowPlayingPresenter>().SingleInstance();
		builder.RegisterType<LargeTrackInfoPresenter>().As<ILargeTrackInfoPresenter>().SingleInstance();
		builder.RegisterType<TrackAboutPresenter>().As<ITrackAboutPresenter>().SingleInstance();
		builder.RegisterType<MyWavePresenter>().As<IMyWavePresenter>().SingleInstance();
		builder.RegisterType<MyWaveWindowPresenter>().As<IMyWaveWindowPresenter>().SingleInstance();
		builder.RegisterType<LocalFolderManagerPresenter>().As<ILocalFolderManagerPresenter>().SingleInstance();

		// Регистрация командной строки (CommandInputView)
		builder.RegisterType<CommandRegistry>().AsSelf().SingleInstance();
		builder.RegisterType<PlayCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<PauseCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<ToggleCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<StopCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<NextCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<PreviousCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<RestartCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<SeekCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<ModeCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<QueueCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<EqualizerCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<VolumeCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<NowCommand>().As<ICommand>().SingleInstance();
		builder.Register(c => new SearchCommand(c.Resolve<IEventBus>(), LocalMusicSource.Id)).As<ICommand>().SingleInstance();
		builder.Register(c => new LikeLocalCommand(c.Resolve<IPlaybackQueue>(), c.Resolve<IEventBus>())).As<ICommand>().SingleInstance();
		builder.RegisterType<ClearCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<UpdateCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<AboutCommand>().As<ICommand>().SingleInstance();

		// Регистрация прикладных сценариев (use case)
		builder.RegisterType<SearchAndLoadPlaylistUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ToggleFavoriteUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ScanLibraryUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ShowMyWaveUseCase>().AsSelf().As<IMyWaveService>().SingleInstance();

		// Регистрация MainWindowCoordinator
		builder.RegisterType<MainWindowCoordinator>().AsSelf().SingleInstance();

		// Регистрация MainWindow
		builder.RegisterType<MainWindow>().AsSelf().SingleInstance();

		// Миграция старого плоского кэша треков (v1.1.0): источник — исторический литерал.
		builder.Register(c => new TrackCacheMigrator(TracksFolder, new SourceId("yandex")))
			.As<ITrackCacheMigrator>()
			.SingleInstance();

		builder.Register(c => new SnapshotFavoriteImporter(
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlist_state.json"),
				c.Resolve<IPluginStateFile>()))
			.As<ISnapshotFavoriteImporter>()
			.SingleInstance();

		Ioc = builder.Build();

		// Schema creation/migration runs once, before any service touches the database.
		Ioc.Resolve<IDatabaseInitializer>().Initialize();

		// Избранное из снимка v1 нужно источнику до первого чтения состояния.
		Ioc.Resolve<ISnapshotFavoriteImporter>().ImportIfMissing();

		// Старый плоский кэш раскладывается по источникам один раз.
		Ioc.Resolve<ITrackCacheMigrator>().Migrate();
	}
}
