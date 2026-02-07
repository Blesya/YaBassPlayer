using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IPlaylistsView
{
	event Action<Playlist>? PlaylistSelected;
	void SetPlaylistTree(IEnumerable<PlaylistTreeItem> roots);
}