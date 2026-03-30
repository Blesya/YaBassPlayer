using Terminal.Gui;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public sealed class LocalFolderManagerPresenter : ILocalFolderManagerPresenter
{
    private readonly ILocalFolderManagerView _view;
    private readonly ILocalLibraryService _libraryService;

    public LocalFolderManagerPresenter(ILocalFolderManagerView view, ILocalLibraryService libraryService)
    {
        _view = view;
        _libraryService = libraryService;
    }

    public event Action? OnLibraryChanged;

    public async Task ShowAsync()
    {
        var folders = await _libraryService.GetFoldersAsync();
        _view.SetFolders(folders);

        _view.OnAddFolderClicked += HandleAddFolder;
        _view.OnRemoveFolderClicked += HandleRemoveFolder;
        _view.OnScanFolderClicked += HandleScanFolder;
        _view.OnScanAllClicked += HandleScanAll;
        _view.OnCloseClicked += HandleClose;

        _view.Show(); // blocks until dialog closes

        _view.OnAddFolderClicked -= HandleAddFolder;
        _view.OnRemoveFolderClicked -= HandleRemoveFolder;
        _view.OnScanFolderClicked -= HandleScanFolder;
        _view.OnScanAllClicked -= HandleScanAll;
        _view.OnCloseClicked -= HandleClose;
    }

    private async void HandleAddFolder()
    {
        try
        {
            var od = new OpenDialog("Выбрать папку", "Выберите папку с музыкой")
            {
                CanChooseDirectories = true,
                CanChooseFiles = false
            };
            Application.Run(od);

            if (!od.Canceled && od.FilePath != null)
            {
                string path = od.FilePath.ToString()!;
                _view.ShowScanProgress("Сканирование...");

                var progress = new Progress<string>(f =>
                    Application.MainLoop.Invoke(() =>
                        _view.ShowScanProgress($"Сканирование: {Path.GetFileName(f)}")));

                await _libraryService.AddFolderAsync(path); // AddFolderAsync already scans
                var folders = await _libraryService.GetFoldersAsync();

                Application.MainLoop.Invoke(() =>
                {
                    _view.SetFolders(folders);
                    _view.ShowScanCompleted(0);
                });

                OnLibraryChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Ошибка", $"Не удалось добавить папку: {ex.Message}", "OK");
        }
    }

    private async void HandleRemoveFolder(int folderId)
    {
        try
        {
            int result = MessageBox.Query("Удалить?", "Удалить папку и все её треки из библиотеки?", "Да", "Нет");
            if (result != 0)
                return;

            await _libraryService.RemoveFolderAsync(folderId);

            await RefreshFoldersAsync();
            OnLibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Ошибка", $"Не удалось удалить папку: {ex.Message}", "OK");
        }
    }

    private async void HandleScanFolder(int folderId)
    {
        try
        {
            _view.ShowScanProgress("Сканирование...");

            var progress = new Progress<string>(f =>
                Application.MainLoop.Invoke(() =>
                    _view.ShowScanProgress($"Сканирование: {Path.GetFileName(f)}")));

            int count = await _libraryService.ScanFolderAsync(folderId, progress);

            Application.MainLoop.Invoke(() =>
            {
                _view.ShowScanCompleted(count);
                RefreshFolders();
            });

            OnLibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Ошибка", $"Не удалось просканировать папку: {ex.Message}", "OK");
        }
    }

    private async void HandleScanAll()
    {
        try
        {
            _view.ShowScanProgress("Сканирование...");

            var progress = new Progress<string>(f =>
                Application.MainLoop.Invoke(() =>
                    _view.ShowScanProgress($"Сканирование: {Path.GetFileName(f)}")));

            int count = await _libraryService.ScanAllFoldersAsync(progress);

            Application.MainLoop.Invoke(() =>
            {
                _view.ShowScanCompleted(count);
                RefreshFolders();
            });

            OnLibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Ошибка", $"Не удалось просканировать папки: {ex.Message}", "OK");
        }
    }

    private void HandleClose()
    {
        _view.Close();
    }

    private async void RefreshFolders()
    {
        await RefreshFoldersAsync();
    }

    private async Task RefreshFoldersAsync()
    {
        var folders = await _libraryService.GetFoldersAsync();
        Application.MainLoop.Invoke(() => _view.SetFolders(folders));
    }
}
