using YamBassPlayer.Models;

namespace YamBassPlayer.Services
{
	public interface ITracksService
	{
		Task SetPlaylist(Playlist playlist);
		Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch);
	}
}
