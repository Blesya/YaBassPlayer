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
		builder.RegisterType<AudioPlayerService>().As<IAudioPlayer>().SingleInstance();
		builder.RegisterType<BassEqualizer>().As<IBassEqualizer>().SingleInstance();
		builder.RegisterType<DatabaseProvider>().As<IDatabaseProvider>().SingleInstance();
		builder.RegisterType<YandexRadioService>().As<IYandexRadioService>().SingleInstance();
			
		builder.Register(c => c.Resolve<IDatabaseProvider>().Connection)
			.As<SqliteConnection>()
			.SingleInstance();
			
		builder.RegisterType<HistoryService>().As<IHistoryService>().SingleInstance();
		builder.RegisterType<RecommendationService>().As<IRecommendationService>().SingleInstance();
		builder.RegisterType<LocalFavoriteService>().As<ILocalFavoriteService>().SingleInstance();
		builder.RegisterType<YandexFavoriteService>().As<IYandexFavoriteService>().SingleInstance();
		builder.RegisterType<ListenTimer>().As<IListenTimer>().SingleInstance();
		builder.RegisterType<PlaybackQueue>().As<IPlaybackQueue>().SingleInstance();
			
		builder.Register(c => new TrackFileProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			TracksFolder
		)).As<ITrackFileProvider>().SingleInstance();

		builder.Register(c => new CoverProvider(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			CoversFolder
		)).As<ICoverProvider>().SingleInstance();
			
		builder.RegisterType<TrackInfoProvider>().As<ITrackInfoProvider>().SingleInstance();
		builder.RegisterType<LyricsService>().As<ILyricsService>().SingleInstance();
		
		builder.Register(c => new DatabaseStatisticsService(
			c.Resolve<SqliteConnection>(),
			TracksFolder
		)).As<IDatabaseStatisticsService>().SingleInstance();
			
		builder.Register(c => new TrackRepository(
			c.Resolve<YandexMusicApi>(),
			c.Resolve<AuthStorage>(),
			c.Resolve<ITrackInfoProvider>(),
			TracksFolder,
			c.Resolve<IHistoryService>(),
			c.Resolve<ILocalFavoriteService>(),
			c.Resolve<IYandexFavoriteService>()
		)).As<ITrackRepository>().SingleInstance();

		// Регистрация Views
		builder.RegisterType<PlayStatusView>().As<IPlayStatusView>().AsSelf().SingleInstance();
		builder.RegisterType<PlaylistsView>().As<IPlaylistsView>().AsSelf().SingleInstance();
		builder.RegisterType<TracksTileView>().As<ITracksView>().AsSelf().SingleInstance();
		builder.RegisterType<TrackInfoPanelView>().As<ITrackInfoPanelView>().AsSelf().SingleInstance();
		builder.RegisterType<LocalSearchView>().As<ILocalSearchView>();
		builder.RegisterType<YandexSearchView>().As<IYandexSearchView>();
		builder.RegisterType<LargeTrackInfoView>().As<ILargeTrackInfoView>();

		// Регистрация Presenters
		builder.RegisterType<PlayStatusPresenter>().As<IPlayStatusPresenter>().SingleInstance();
		builder.RegisterType<PlaylistsPresenter>().As<IPlaylistsPresenter>().SingleInstance();
		builder.RegisterType<TracksPresenter>().As<ITracksPresenter>().SingleInstance();
		builder.RegisterType<TrackInfoPanelPresenter>().As<ITrackInfoPanelPresenter>().SingleInstance();
		builder.RegisterType<EqualizerPresenter>().As<IEqualizerPresenter>().SingleInstance();
		builder.RegisterType<LocalSearchPresenter>().As<ILocalSearchPresenter>().SingleInstance();
		builder.RegisterType<YandexSearchPresenter>().As<IYandexSearchPresenter>().SingleInstance();
		builder.RegisterType<DatabaseStatisticsPresenter>().As<IDatabaseStatisticsPresenter>().SingleInstance();
		builder.RegisterType<NowPlayingPresenter>().As<INowPlayingPresenter>().SingleInstance();
		builder.RegisterType<LargeTrackInfoPresenter>().As<ILargeTrackInfoPresenter>().SingleInstance();
		builder.RegisterType<OnSameWavePresenter>().As<IOnSameWavePresenter>().SingleInstance();
		builder.RegisterType<RecommendationGraphPresenter>().As<IRecommendationGraphPresenter>().SingleInstance();
		builder.RegisterType<MyWavePresenter>().As<IMyWavePresenter>().SingleInstance();

		// Регистрация MainWindow
		builder.RegisterType<MainWindow>().AsSelf().SingleInstance();

		Ioc = builder.Build();
	}
}