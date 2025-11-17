using YamBassPlayer.Models;

namespace YamBassPlayer.Services
{
	public interface IPlaylistsService
	{
		Task<IEnumerable<Playlist>> GetPlaylists();
	}
}
