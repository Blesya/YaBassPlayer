using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Presenters;

public interface IPlaylistsPresenter
{
	event Action<Playlist>? PlaylistChosen;
	void NotifyTransientPlaylistActive(Playlist playlist);

	/// <summary>Initializes and loads the playlist tree. Call once after WireEvents.</summary>
	Task InitializeAsync(CancellationToken ct = default);

	/// <summary>Reloads the playlist tree from all registered music sources.</summary>
	void LoadPlaylistTree();
}