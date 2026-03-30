using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ILocalFolderManagerView
{
	void SetFolders(IReadOnlyList<LocalFolder> folders);
	void ShowScanProgress(string message);
	void ShowScanCompleted(int newTracksCount);
	void Show();
	void Close();

	event Action? OnAddFolderClicked;
	event Action<int>? OnRemoveFolderClicked;  // folderId
	event Action<int>? OnScanFolderClicked;    // folderId
	event Action? OnScanAllClicked;
	event Action? OnCloseClicked;
}
