using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public interface IPlaylistsView
{
	event Action<int>? PlaylistSelected;
	void SetPlaylists(IEnumerable<Playlist> playlists);
	void HighlightPlaylist(int index);
}