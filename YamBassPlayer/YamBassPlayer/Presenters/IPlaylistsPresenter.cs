using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IPlaylistsPresenter
{
	event Action<Playlist>? PlaylistChosen;
	void NotifyTransientPlaylistActive(Playlist playlist);

	/// <summary>Reloads the playlist tree from all registered music sources.</summary>
	void LoadPlaylistTree();
}