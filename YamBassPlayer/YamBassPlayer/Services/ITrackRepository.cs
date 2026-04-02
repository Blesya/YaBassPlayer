using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackRepository
{
    Task<IEnumerable<Playlist>> GetPlaylists();
    Task SetPlaylist(Playlist playlist);
    Task<IEnumerable<Track>> GetNextTracks(int tracksPerBatch);

    IReadOnlyList<string> GetAllTrackIds();
    PlaylistType? CurrentPlaylistType { get; }

    Task<IEnumerable<Track>> GetCachedTracksOrMinimum(int minCount);

    void UpdateLocalSearchCache(IEnumerable<Track> tracks);
    void UpdateYandexSearchCache(IEnumerable<Track> tracks);
    void UpdateQueueCache(IEnumerable<string> trackIds);
    void UpdateOnSameWaveCache(IEnumerable<Track> tracks);
    void UpdateMyWaveCache(IEnumerable<Track> tracks);
    void AppendMyWaveCache(IEnumerable<Track> tracks);
}
