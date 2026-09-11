using Terminal.Gui;
using YamBassPlayer.Extensions;
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

        Action onAddFolder = () => HandleAddFolder().Forget();
        Action<int> onRemoveFolder = id => HandleRemoveFolder(id).Forget();
        Action<int> onScanFolder = id => HandleScanFolder(id).Forget();
        Action onScanAll = () => HandleScanAll().Forget();

        _view.OnAddFolderClicked += onAddFolder;
        _view.OnRemoveFolderClicked += onRemoveFolder;
        _view.OnScanFolderClicked += onScanFolder;
        _view.OnScanAllClicked += onScanAll;
        _view.OnCloseClicked += HandleClose;

        _view.Show(); // blocks until dialog closes

        _view.OnAddFolderClicked -= onAddFolder;
        _view.OnRemoveFolderClicked -= onRemoveFolder;
        _view.OnScanFolderClicked -= onScanFolder;
        _view.OnScanAllClicked -= onScanAll;
        _view.OnCloseClicked -= HandleClose;
    }

    private async Task HandleAddFolder()
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

    private async Task HandleRemoveFolder(int folderId)
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

    private async Task HandleScanFolder(int folderId)
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
                RefreshFolders().Forget();
            });

            OnLibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Ошибка", $"Не удалось просканировать папку: {ex.Message}", "OK");
        }
    }

    private async Task HandleScanAll()
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
                RefreshFolders().Forget();
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

    private async Task RefreshFolders()
    {
        await RefreshFoldersAsync();
    }

    private async Task RefreshFoldersAsync()
    {
        var folders = await _libraryService.GetFoldersAsync();
        Application.MainLoop.Invoke(() => _view.SetFolders(folders));
    }
}
