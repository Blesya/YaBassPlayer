using YamBassPlayer.Presenters;
using YamBassPlayer.Services;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Прикладной сценарий работы с локальной библиотекой: добавление папки, управление
/// папками и сканирование. Обновление дерева плейлистов маршалится в UI-поток.
/// </summary>
public sealed class ScanLibraryUseCase
{
	private readonly ILocalLibraryService _libraryService;
	private readonly ILocalFolderManagerPresenter _folderManagerPresenter;
	private readonly IPlaylistsPresenter _playlistsPresenter;
	private readonly IUiDispatcher _uiDispatcher;
	private readonly IErrorHandler _errorHandler;

	public ScanLibraryUseCase(
		ILocalLibraryService libraryService,
		ILocalFolderManagerPresenter folderManagerPresenter,
		IPlaylistsPresenter playlistsPresenter,
		IUiDispatcher uiDispatcher,
		IErrorHandler errorHandler)
	{
		_libraryService = libraryService;
		_folderManagerPresenter = folderManagerPresenter;
		_playlistsPresenter = playlistsPresenter;
		_uiDispatcher = uiDispatcher;
		_errorHandler = errorHandler;
	}

	/// <summary>Добавляет папку в библиотеку и обновляет дерево плейлистов.</summary>
	public async Task AddFolderAsync(string path)
	{
		try
		{
			await _libraryService.AddFolderAsync(path);
			_uiDispatcher.Invoke(RefreshPlaylistTree);
		}
		catch (Exception ex)
		{
			_uiDispatcher.Invoke(() => _errorHandler.Handle(ex));
		}
	}

	/// <summary>Показывает окно управления папками и обновляет дерево при изменениях.</summary>
	public async Task ShowFolderManagerAsync()
	{
		Action onLibraryChanged = RefreshPlaylistTree;
		_folderManagerPresenter.OnLibraryChanged += onLibraryChanged;
		try { await _folderManagerPresenter.ShowAsync(); }
		finally { _folderManagerPresenter.OnLibraryChanged -= onLibraryChanged; }
	}

	/// <summary>
	/// Сканирует все папки библиотеки и обновляет дерево плейлистов.
	/// Возвращает количество найденных треков, либо null при ошибке (диалог уже показан).
	/// </summary>
	public async Task<int?> ScanAllFoldersAsync()
	{
		try
		{
			int count = await _libraryService.ScanAllFoldersAsync();
			_uiDispatcher.Invoke(RefreshPlaylistTree);
			return count;
		}
		catch (Exception ex)
		{
			_uiDispatcher.Invoke(() => _errorHandler.Handle(ex));
			return null;
		}
	}

	public void RefreshPlaylistTree() => _playlistsPresenter.LoadPlaylistTree();
}
