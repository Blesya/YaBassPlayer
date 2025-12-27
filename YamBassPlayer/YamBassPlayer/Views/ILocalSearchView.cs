using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ILocalSearchView
{
    event Action? OnOkClicked;
    event Action? OnCancelClicked;
    event Action<string>? OnSearchQueryChanged;
    
    void SetSearchResults(IEnumerable<Track> tracks);
    void Show();
    void Close();
    void ShowError(string message);
}
