using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IYandexSearchView
{
	event Action<string>? OnSearchClicked;
	event Action? OnOkClicked;
	event Action? OnCancelClicked;

	void SetSearchResults(IEnumerable<Track> tracks);
	void SetLoading(bool isLoading);
	void Show();
	void Close();
	void ShowError(string message);
}
