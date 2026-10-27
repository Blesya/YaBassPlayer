using Serilog;
using YamBassPlayer.Api;
using YamBassPlayer.Api.Common;
using YamBassPlayer.Enums;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Плагин Яндекс.Музыки: источник, поиск, «Моя волна» и вход через настройку.
/// Собирается и грузится из <c>plugins/yandex-music/</c>, поэтому сам создаёт
/// свои службы и берёт у хоста только то, что объявлено в контрактах.
/// </summary>
public sealed class YandexMusicPlugin : IPlugin, IMenuContributor
{
    private IPluginUi? _ui;
    private IPluginSettings? _settings;
    private IErrorHandler? _errorHandler;
    private IMyWaveService? _myWaveService;
    private ITrackInfoProvider? _trackInfoProvider;
    private IAuthService? _authService;
    private IYandexSearchPresenter? _searchPresenter;

    public string Id => "yandex-music";

    public string DisplayName => "Яндекс.Музыка";

    public event Action? Changed;

    public void Register(IPluginRegistrar registrar)
    {
        _ui = (registrar as IPluginUiRegistrar)?.Ui;
        _settings = registrar.Settings;
        _errorHandler = registrar.Host.Errors;
        _myWaveService = registrar.Host.MyWave;
        _trackInfoProvider = registrar.Host.TrackInfo;

        _authService = new AuthService(new DeviceAuthorizationService(_settings), _settings);

        YandexMusicApi api = _authService.Api;
        AuthStorage storage = _authService.Storage;

        var favorites = new YandexFavoriteService(api, storage, registrar.State);
        favorites.OnFavoriteAdded += _ => Changed?.Invoke();
        favorites.OnFavoriteRemoved += _ => Changed?.Invoke();

        var radio = new YandexRadioService(api, storage);
        var source = new YandexMusicSource(
            api,
            storage,
            registrar.Host.TracksFolder,
            registrar.Host.CoversFolder,
            favorites,
            radio);

        _searchPresenter = _ui is null
            ? null
            : new YandexSearchPresenter(source, () => new YandexSearchView(), _ui.Dispatcher);

        registrar.AddMusicSource(source);
        registrar.AddTrackAction(new YandexLikeTrackAction(favorites));
        registrar.AddCommand(new YandexLikeCommand(registrar.Host.Playback, favorites));
        registrar.AddCommand(new YandexSearchCommand(ShowSearchAsync));
        registrar.AddMenu(this);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_authService is null)
            return;

        // Тихое восстановление сессии: без токена источник остаётся в RequiresSetup.
        if (!_authService.HasStoredToken && !_authService.HasStoredRefreshToken)
            return;

        try
        {
            if (_authService.HasStoredToken && await _authService.AuthorizeFromConfigAsync())
            {
                Changed?.Invoke();
                return;
            }

            if (_authService.HasStoredRefreshToken && await _authService.TryRefreshAsync())
                Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось восстановить сессию Яндекс.Музыки");
        }
    }

    public Task ConfigureAsync(CancellationToken cancellationToken)
    {
        if (_ui is null || _authService is null || _settings is null || _errorHandler is null)
            return Task.CompletedTask;

        var presenter = new AuthPresenter(
            _authService,
            new QrCodeRenderer(),
            new AuthView(),
            _ui.Dispatcher,
            _errorHandler,
            _settings);

        presenter.Run();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public IReadOnlyList<PluginMenuEntry> GetEntries() =>
    [
        new("Поиск", ShowSearchAsync),
        new("Моя волна", ShowMyWaveAsync, Shortcut: "F9"),
        new("Моя волна по треку", ShowMyWaveByTrackAsync),
    ];

    private async Task ShowSearchAsync()
    {
        if (_searchPresenter is null || _trackInfoProvider is null || _ui is null)
            return;

        await _searchPresenter.ShowYandexSearchDialogAsync();
        if (_searchPresenter.WasCancelled())
            return;

        var selected = _searchPresenter.GetSelectedTracks();
        if (selected.Count == 0)
            return;

        foreach (var track in selected)
            await _trackInfoProvider.SaveAsync(track);

        var playlist = new Playlist("search", "Поиск по ЯМ")
        {
            SourceId = YandexMusicSourceId.Value,
            Category = PlaylistCategory.Transient,
            Description = "Результаты поиска по Яндекс.Музыке",
            TrackCount = selected.Count
        };

        await _ui.ShowPlaylistAsync(playlist, selected);
    }

    private Task ShowMyWaveAsync()
    {
        if (_myWaveService is not null)
            _myWaveService.ShowAsync(_ => { }).Forget();

        return Task.CompletedTask;
    }

    private Task ShowMyWaveByTrackAsync()
    {
        if (_myWaveService is not null)
            _myWaveService.ShowByTrackAsync(_ => { }).Forget();

        return Task.CompletedTask;
    }
}
