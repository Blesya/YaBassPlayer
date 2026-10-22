using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.UseCases;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Встроенный плагин локальной музыки: источник, папки, сканирование и поиск.
/// </summary>
public sealed class LocalMusicPlugin(
    LocalMusicSource source,
    ILocalSearchPresenter localSearchPresenter,
    ScanLibraryUseCase scanLibraryUseCase,
    SearchAndLoadPlaylistUseCase searchAndLoadPlaylistUseCase) : IPlugin, IMenuContributor
{
    private IPluginUi? _ui;

    public string Id => "local";
    public string DisplayName => "Локальная музыка";

    public event Action? Changed;

    public void Register(IPluginRegistrar registrar)
    {
        _ui = (registrar as IPluginUiRegistrar)?.Ui;
        registrar.AddMusicSource(source);
        registrar.AddMenu(this);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ConfigureAsync(CancellationToken cancellationToken)
    {
        scanLibraryUseCase.ShowFolderManagerAsync().Forget();
        return Task.CompletedTask;
    }

    public IReadOnlyList<PluginMenuEntry> GetEntries() =>
    [
        new("Локальный поиск", ShowSearchAsync),
        new("Добавить папку…", AddFolderAsync),
        new("Управление папками…", ShowFoldersAsync),
        new("Сканировать библиотеку", ScanAsync),
    ];

    private async Task ShowSearchAsync()
    {
        localSearchPresenter.ShowLocalSearchDialog();
        if (localSearchPresenter.WasCancelled())
            return;

        IReadOnlyList<Track> selected = localSearchPresenter.GetSelectedTracks();
        if (selected.Count > 0)
            await searchAndLoadPlaylistUseCase.LoadSelectionAsync(LocalMusicSource.Id, selected, _ => { });
    }

    private Task ShowFoldersAsync()
    {
        scanLibraryUseCase.ShowFolderManagerAsync().Forget();
        return Task.CompletedTask;
    }

    private Task AddFolderAsync()
    {
        var dialog = new OpenDialog("Добавить папку", "Выберите папку с музыкой")
        {
            CanChooseDirectories = true,
            CanChooseFiles = false
        };
        Application.Run(dialog);

        if (!dialog.Canceled && dialog.FilePath is { } path)
            _ = scanLibraryUseCase.AddFolderAsync(path.ToString()!);

        return Task.CompletedTask;
    }

    private async Task ScanAsync()
    {
        int? count = await scanLibraryUseCase.ScanAllFoldersAsync();
        if (count is { } found)
            _ui?.SetStatus($"Сканирование завершено: найдено треков {found}");
    }
}
