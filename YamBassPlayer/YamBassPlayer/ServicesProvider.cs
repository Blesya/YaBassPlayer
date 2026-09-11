using Autofac;
using YamBassPlayer.Commands;
using YamBassPlayer.Configuration;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.UseCases;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer;

public static class ServicesProvider
{
	private static readonly string TracksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracks");
	private static readonly string CoversFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "covers");
		
	public static IContainer Ioc { get; private set; } = null!;

	public static void Initialise(IAuthService authService)
	{
		var builder = new ContainerBuilder();

		// Регистрация внешних зависимостей
		builder.RegisterInstance(authService).As<IAuthService>().SingleInstance();
		builder.RegisterInstance(authService.Api).As<YandexMusicApi>().SingleInstance();
		builder.RegisterInstance(authService.Storage).As<AuthStorage>().SingleInstance();

		// Регистрация сервисов
		builder.RegisterType<AudioPlayerService>().As<IAudioPlayer>().SingleInstance();
		builder.RegisterType<BassEqualizer>().As<IBassEqualizer>().SingleInstance();
		builder.RegisterType<CoverArtService>().As<ICoverArtService>().SingleInstance();
		builder.RegisterType<SqliteConnectionFactory>().As<IDbConnectionFactory>().SingleInstance();
		builder.RegisterType<DatabaseInitializer>().As<IDatabaseInitializer>().SingleInstance();
		builder.RegisterType<DbWriteLock>().As<IDbWriteLock>().SingleInstance();
		builder.RegisterType<AppConfigurationProvider>().As<IAppConfiguration>().SingleInstance();
		builder.RegisterType<UiErrorHandler>().As<IErrorHandler>().SingleInstance();
		builder.RegisterType<UiDispatcher>().As<IUiDispatcher>().SingleInstance();
		builder.RegisterType<EventBus>().As<IEventBus>().SingleInstance();
		builder.RegisterType<YandexRadioService>().As<IYandexRadioService>().SingleInstance();
			
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
		builder.RegisterType<PlaybackPresenter>().As<IPlaybackPresenter>().SingleInstance();
		builder.RegisterType<LocalFavoriteService>()
			.As<ILocalFavoriteService>()
			.As<ITrackFavoriteSourceService>()
			.SingleInstance();
		builder.RegisterType<YandexFavoriteService>()
			.As<IYandexFavoriteService>()
			.As<ITrackFavoriteSourceService>()
			.SingleInstance();
		builder.RegisterType<TrackFavoriteService>().As<ITrackFavoriteService>().SingleInstance();
		builder.RegisterType<ListenTimer>().As<IListenTimer>().SingleInstance();
		builder.RegisterType<PlaybackQueue>().As<IPlaybackQueue>().SingleInstance();
			
		builder.Register(c => new TrackFileProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			TracksFolder,
			c.Resolve<ITrackSourceDetector>()
		)).As<ITrackFileProvider>().SingleInstance();

		builder.Register(c => new CoverProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			CoversFolder,
			c.Resolve<IDbConnectionFactory>()
		)).As<ICoverProvider>().SingleInstance();
			
		builder.RegisterType<TrackInfoProvider>().As<ITrackInfoProvider>().SingleInstance();
		builder.RegisterType<TrackCatalog>().As<ITrackCatalog>().SingleInstance();
		builder.RegisterType<SourceSearchService>().As<ISourceSearchService>().SingleInstance();
		builder.Register(c => new LyricsService(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			c.Resolve<IDbConnectionFactory>(),
			c.Resolve<IDbWriteLock>()
		)).As<ILyricsService>().SingleInstance();
		builder.RegisterType<SourcesBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<TopByDayBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<GlobalArtistsBranchBuilder>().As<ITreeBranchBuilder>().SingleInstance();
		builder.RegisterType<PlaylistTreeComposer>().As<IPlaylistTreeComposer>().SingleInstance();
		builder.RegisterType<TrackSourceDetector>().As<ITrackSourceDetector>().SingleInstance();

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
		builder.RegisterType<FavoritesLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<Top10LoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<TopEveningsLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<TopByDayLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.Register(c => new CachedLoadStrategy(TracksFolder)).As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<PlaylistOfTheDayLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<CustomLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<ArtistLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<QueueLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<MyWaveLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<LocalSearchLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<YandexSearchLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<LocalFolderLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<LocalArtistLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<LocalAlbumLoadStrategy>().As<IPlaylistLoadStrategy>().SingleInstance();
		builder.RegisterType<PlaylistLoadStrategyResolver>().AsSelf().SingleInstance();

		builder.Register(c => new AppPlaylistProvider(
			c.Resolve<ILocalFavoriteService>(),
			TracksFolder
		)).As<IAppPlaylistProvider>().SingleInstance();

		builder.Register(c => new YandexPlaylistInitializer(
			c.Resolve<IMusicSourceRegistry>(),
			c.Resolve<IYandexFavoriteService>(),
			c.Resolve<ITrackRepositoryCache>(),
			c.Resolve<ITrackInfoProvider>()
		)).As<IYandexPlaylistInitializer>().SingleInstance();

		builder.Register(c => new TrackRepository(
			c.Resolve<IMusicSourceRegistry>(),
			c.Resolve<ITrackInfoProvider>(),
			c.Resolve<IHistoryService>(),
			c.Resolve<ITrackRepositoryCache>(),
			c.Resolve<ILocalLibraryService>(),
			c.Resolve<PlaylistLoadStrategyResolver>(),
			c.Resolve<IAppPlaylistProvider>(),
			c.Resolve<IYandexPlaylistInitializer>()
		)).As<ITrackRepository>().SingleInstance();

		// Регистрация источников музыки
		builder.RegisterType<MusicSourceRegistry>().As<IMusicSourceRegistry>().SingleInstance();

		builder.Register(c => new YandexMusicSource(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			TracksFolder,
			CoversFolder
		)).Named<IMusicSource>(YamBassPlayer.Models.SourceIds.Yandex).As<IMusicSource>().SingleInstance();

		builder.RegisterType<LocalMusicSource>().Named<IMusicSource>(YamBassPlayer.Models.SourceIds.Local).As<IMusicSource>().SingleInstance();

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
		builder.RegisterType<YandexSearchView>().As<IYandexSearchView>();
		builder.RegisterType<LargeTrackInfoView>().As<ILargeTrackInfoView>();
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
		builder.RegisterType<YandexSearchPresenter>().As<IYandexSearchPresenter>().SingleInstance();
		builder.RegisterType<DatabaseStatisticsPresenter>().As<IDatabaseStatisticsPresenter>().SingleInstance();
		builder.RegisterType<NowPlayingPresenter>().As<INowPlayingPresenter>().SingleInstance();
		builder.RegisterType<LargeTrackInfoPresenter>().As<ILargeTrackInfoPresenter>().SingleInstance();
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
		builder.RegisterType<NowCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<SearchCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<LikeYandexCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<LikeLocalCommand>().As<ICommand>().SingleInstance();
		builder.RegisterType<ClearCommand>().As<ICommand>().SingleInstance();

		// Регистрация прикладных сценариев (use case)
		builder.RegisterType<SearchAndLoadPlaylistUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ToggleFavoriteUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ScanLibraryUseCase>().AsSelf().SingleInstance();
		builder.RegisterType<ShowMyWaveUseCase>().AsSelf().SingleInstance();

		// Регистрация MainWindowCoordinator
		builder.RegisterType<MainWindowCoordinator>().AsSelf().SingleInstance();

		// Регистрация MainWindow
		builder.RegisterType<MainWindow>().AsSelf().SingleInstance();

		Ioc = builder.Build();

		// Schema creation/migration runs once, before any service touches the database.
		Ioc.Resolve<IDatabaseInitializer>().Initialize();
	}
}
