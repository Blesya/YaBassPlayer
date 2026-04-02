using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IPlaylistTreeComposer
{
	/// <summary>
	/// Builds the playlists tree shown in the UI from the current playlist set.
	/// </summary>
	Task<IReadOnlyList<PlaylistTreeItem>> ComposeAsync(IReadOnlyList<Playlist> playlists);
}
