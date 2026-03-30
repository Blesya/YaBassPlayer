namespace YamBassPlayer.Presenters;

public interface ILocalFolderManagerPresenter
{
    Task ShowAsync();
    event Action? OnLibraryChanged;
}
