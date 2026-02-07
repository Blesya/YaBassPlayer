using YamBassPlayer.Models;

namespace YamBassPlayer.Services;
	
public interface ITrackRepository
{
	Task<IEnumerable<Playlist>> GetPlaylists();
	Task<IEnumerable<PlaylistTreeItem>> GetPlaylistTree();
	Task SetPlaylist(Playlist playlist);
	Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch);
		
	IReadOnlyList<string> GetAllTrackIds();

	Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount);

	void UpdateLocalSearchCache(IEnumerable<Track> tracks);
}