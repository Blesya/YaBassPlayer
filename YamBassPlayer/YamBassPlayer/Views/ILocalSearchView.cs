using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface ILocalSearchView
{
	event Action? OnOkClicked;
	event Action? OnCancelClicked;
	event Action<string>? OnSearchQueryChanged;
	
	void SetSearchResults(IEnumerable<Track> tracks);

	/// <summary>
	/// Returns tracks explicitly marked by the user in the results list.
	/// </summary>
	IReadOnlyList<Track> GetMarkedTracks();
	void Show();
	void Close();
	void ShowError(string message);
}
