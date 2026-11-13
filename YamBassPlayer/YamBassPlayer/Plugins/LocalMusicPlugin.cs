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
    SearchAndLoadPlaylistUseCase searchAndLoadPlaylistUseCase,
    IUiDispatcher uiDispatcher) : IPlugin, IMenuContributor
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
        => RunOnUiThreadAsync(() => scanLibraryUseCase.ShowFolderManagerAsync().Forget());

    public IReadOnlyList<PluginMenuEntry> GetEntries() =>
    [
        new("Локальный поиск", ShowSearchAsync),
        new("Добавить папку…", AddFolderAsync),
        new("Управление папками…", ShowFoldersAsync),
        new("Сканировать библиотеку", ScanAsync),
    ];

    private async Task ShowSearchAsync()
    {
        await RunOnUiThreadAsync(localSearchPresenter.ShowLocalSearchDialog);
        if (localSearchPresenter.WasCancelled())
            return;

        IReadOnlyList<Track> selected = localSearchPresenter.GetSelectedTracks();
        if (selected.Count > 0)
            await searchAndLoadPlaylistUseCase.LoadSelectionAsync(LocalMusicSource.Id, selected, _ => { });
    }

    private Task ShowFoldersAsync()
        => RunOnUiThreadAsync(() => scanLibraryUseCase.ShowFolderManagerAsync().Forget());

    private async Task AddFolderAsync()
    {
        string? path = null;

        await RunOnUiThreadAsync(() =>
        {
            var dialog = new OpenDialog("Добавить папку", "Выберите папку с музыкой")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false
            };
            Application.Run(dialog);

            if (!dialog.Canceled && dialog.FilePath is { } selectedPath)
                path = selectedPath.ToString();
        });

        if (path is { } folder)
            await scanLibraryUseCase.AddFolderAsync(folder);
    }

    private async Task ScanAsync()
    {
        int? count = await scanLibraryUseCase.ScanAllFoldersAsync();
        if (count is { } found)
            uiDispatcher.Invoke(() => _ui?.SetStatus($"Сканирование завершено: найдено треков {found}"));
    }

    /// <summary>
    /// Пункт меню плагина выполняется в фоновой задаче (<c>MainWindow.RunPluginEntry</c>),
    /// а модальное окно Terminal.Gui можно открывать только в UI-потоке: вложенный цикл
    /// начинал крутить драйвер одновременно с основным, и отрисовка падала
    /// (<c>IndexOutOfRangeException</c> в <c>View.Clear</c>). Поэтому окно показывается
    /// через диспетчер, а его результат забирается после закрытия.
    /// </summary>
    private Task RunOnUiThreadAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        uiDispatcher.Invoke(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }
}
