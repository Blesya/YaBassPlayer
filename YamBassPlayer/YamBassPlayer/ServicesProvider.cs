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
        builder.RegisterType<DatabaseProvider>().As<IDatabaseProvider>().SingleInstance();
			
        builder.Register(c => c.Resolve<IDatabaseProvider>().Connection)
            .As<SqliteConnection>()
            .SingleInstance();
			
        builder.RegisterType<HistoryService>().As<IHistoryService>().SingleInstance();
        builder.RegisterType<ListenTimer>().As<IListenTimer>().SingleInstance();
        builder.RegisterType<PlaybackQueue>().As<IPlaybackQueue>().SingleInstance();
			
        builder.Register(c => new TrackFileProvider(
            c.Resolve<YandexMusicApi>(),
            c.Resolve<AuthStorage>(),
            TracksFolder
        )).As<ITrackFileProvider>().SingleInstance();
			
        builder.Register(c => new TrackInfoProvider(
            c.Resolve<YandexMusicApi>(),
            c.Resolve<AuthStorage>(),
            c.Resolve<SqliteConnection>()
        )).As<ITrackInfoProvider>().SingleInstance();
			
        builder.Register(c => new TrackRepository(
            c.Resolve<YandexMusicApi>(),
            c.Resolve<AuthStorage>(),
            c.Resolve<ITrackInfoProvider>(),
            TracksFolder,
            c.Resolve<IHistoryService>()
        )).As<ITrackRepository>().SingleInstance();

        // Регистрация Views
        builder.RegisterType<PlayStatusView>().As<IPlayStatusView>().AsSelf().SingleInstance();
        builder.RegisterType<PlaylistsView>().As<IPlaylistsView>().AsSelf().SingleInstance();
        builder.RegisterType<TracksView>().As<ITracksView>().AsSelf().SingleInstance();

        // Регистрация Presenters
        builder.RegisterType<PlayStatusPresenter>().As<IPlayStatusPresenter>().SingleInstance();
        builder.RegisterType<PlaylistsPresenter>().As<IPlaylistsPresenter>().SingleInstance();
        builder.RegisterType<TracksPresenter>().As<ITracksPresenter>().SingleInstance();

        // Регистрация MainWindow
        builder.RegisterType<MainWindow>().AsSelf().SingleInstance();

        Ioc = builder.Build();
    }
}