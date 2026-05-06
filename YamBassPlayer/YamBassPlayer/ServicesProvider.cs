using Autofac;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
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
		builder.RegisterSingleton<IAudioPlayer, AudioPlayerService>();
		builder.RegisterSingleton<IBassEqualizer, BassEqualizer>();
		builder.RegisterSingleton<IDatabaseProvider, DatabaseProvider>();
		builder.RegisterSingleton<IDbWriteLock, DbWriteLock>();
		builder.RegisterSingleton<IYandexRadioService, YandexRadioService>();
			
		builder.RegisterSingleton<SqliteConnection>(c => c.Resolve<IDatabaseProvider>().Connection);
			
		builder.RegisterSingleton<IHistoryService, HistoryService>();
		builder.RegisterSingleton<ILocalLibraryService>(c => new LocalLibraryService(
			c.Resolve<SqliteConnection>(),
			CoversFolder,
			c.Resolve<IDbWriteLock>()
		));
		builder.RegisterSingleton<IRecommendationService, RecommendationService>();
		builder.RegisterSingleton<ITrackRepositoryCache, TrackRepositoryCache>();
		builder.RegisterSingleton<IPlaybackCoordinator, PlaybackCoordinator>();
		builder.RegisterType<LocalFavoriteService>()
			.As<ILocalFavoriteService>()
			.As<ITrackFavoriteSourceService>()
			.SingleInstance();
		builder.RegisterType<YandexFavoriteService>()
			.As<IYandexFavoriteService>()
			.As<ITrackFavoriteSourceService>()
			.SingleInstance();
		builder.RegisterSingleton<ITrackFavoriteService, TrackFavoriteService>();
		builder.RegisterSingleton<IListenTimer, ListenTimer>();
		builder.RegisterSingleton<IPlaybackQueue, PlaybackQueue>();
			
		builder.RegisterSingleton<ITrackFileProvider>(c => new TrackFileProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			TracksFolder
		));

		builder.RegisterSingleton<ICoverProvider>(c => new CoverProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			CoversFolder,
			c.Resolve<SqliteConnection>()
		));
			
		builder.RegisterSingleton<ITrackInfoProvider, TrackInfoProvider>();
		builder.RegisterSingleton<ISourceSearchService, SourceSearchService>();
		builder.RegisterSingleton<ILyricsService, LyricsService>();
		builder.RegisterSingleton<IPlaylistTreeComposer, PlaylistTreeComposer>();
		
		builder.RegisterSingleton<IDatabaseStatisticsService>(c => new DatabaseStatisticsService(
			c.Resolve<SqliteConnection>(),
			TracksFolder
		));
			
		builder.RegisterSingleton<ITrackRepository>(c => new TrackRepository(
			c.Resolve<IMusicSourceRegistry>(),
			c.Resolve<ITrackInfoProvider>(),
			TracksFolder,
			c.Resolve<IHistoryService>(),
			c.Resolve<ILocalFavoriteService>(),
			c.Resolve<IYandexFavoriteService>(),
			c.Resolve<ITrackRepositoryCache>(),
			c.Resolve<ILocalLibraryService>()
		));

		// Регистрация источников музыки
		builder.RegisterSingleton<IMusicSourceRegistry, MusicSourceRegistry>();

		builder.RegisterNamedSingleton<IMusicSource>("yandex", c => new YandexMusicSource(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			TracksFolder,
			CoversFolder
		));

		builder.RegisterType<LocalMusicSource>().Named<IMusicSource>("local").As<IMusicSource>().SingleInstance();

		// Регистрация Views
		builder.RegisterType<PlayStatusView>().As<IPlayStatusView>().AsSelf().SingleInstance();
		builder.RegisterType<PlaylistsView>().As<IPlaylistsView>().AsSelf().SingleInstance();
		builder.RegisterType<TracksTileView>().As<ITracksView>().AsSelf().SingleInstance();
		builder.RegisterType<TrackInfoPanelView>().As<ITrackInfoPanelView>().AsSelf().SingleInstance();
		builder.RegisterType<LocalSearchView>().As<ILocalSearchView>();
		builder.RegisterType<YandexSearchView>().As<IYandexSearchView>();
		builder.RegisterType<LargeTrackInfoView>().As<ILargeTrackInfoView>();
		builder.RegisterSingleton<ILocalFolderManagerView, LocalFolderManagerView>();

		// Регистрация Presenters
		builder.RegisterSingleton<IPlayStatusPresenter, PlayStatusPresenter>();
		builder.RegisterSingleton<IPlaylistsPresenter, PlaylistsPresenter>();
		builder.RegisterSingleton<ITracksPresenter, TracksPresenter>();
		builder.RegisterSingleton<ITrackInfoPanelPresenter, TrackInfoPanelPresenter>();
		builder.RegisterSingleton<IEqualizerPresenter, EqualizerPresenter>();
		builder.RegisterSingleton<ILocalSearchPresenter, LocalSearchPresenter>();
		builder.RegisterSingleton<IYandexSearchPresenter, YandexSearchPresenter>();
		builder.RegisterSingleton<IDatabaseStatisticsPresenter, DatabaseStatisticsPresenter>();
		builder.RegisterSingleton<INowPlayingPresenter, NowPlayingPresenter>();
		builder.RegisterSingleton<ILargeTrackInfoPresenter, LargeTrackInfoPresenter>();
		builder.RegisterSingleton<IOnSameWavePresenter, OnSameWavePresenter>();
		builder.RegisterSingleton<IRecommendationGraphPresenter, RecommendationGraphPresenter>();
		builder.RegisterSingleton<IMyWavePresenter, MyWavePresenter>();
		builder.RegisterSingleton<IMyWaveWindowPresenter, MyWaveWindowPresenter>();
		builder.RegisterSingleton<ILocalFolderManagerPresenter, LocalFolderManagerPresenter>();

		// Регистрация MainWindow
		builder.RegisterSingletonSelf<MainWindow>();

		Ioc = builder.Build();
	}
}
