using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IYandexSearchView
{
	event Action<string>? OnSearchClicked;
	event Action? OnOkClicked;
	event Action? OnCancelClicked;

	void SetSearchResults(IEnumerable<Track> tracks);

	/// <summary>
	/// Returns tracks explicitly marked by the user in the results list.
	/// </summary>
	IReadOnlyList<Track> GetMarkedTracks();
	void SetLoading(bool isLoading);
	void Show();
	void Close();
	void ShowError(string message);
}
