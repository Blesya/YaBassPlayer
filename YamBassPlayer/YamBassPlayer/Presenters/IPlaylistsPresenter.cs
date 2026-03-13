using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IPlaylistsPresenter
{
	event Action<Playlist>? PlaylistChosen;
	void NotifyTransientPlaylistActive(Playlist playlist);
}