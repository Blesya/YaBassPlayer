using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IYandexSearchView
{
	event Action<string>? OnSearchClicked;
	event Action? OnOkClicked;
	event Action? OnCancelClicked;

	void SetSearchResults(SearchResult result);

	/// <summary>
	/// Returns the items (tracks, artists, albums) explicitly marked by the user.
	/// </summary>
	IReadOnlyList<SearchResultItem> GetMarkedItems();
	void SetLoading(bool isLoading);
	void Show();
	void Close();
	void ShowError(string message);
}
