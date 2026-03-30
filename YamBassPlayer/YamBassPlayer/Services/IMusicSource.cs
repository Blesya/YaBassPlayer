using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IMusicSource
{
    string SourceId { get; }
    string DisplayName { get; }
    bool SupportsSearch { get; }
    bool SupportsFavorites { get; }

    Task<IEnumerable<Playlist>> GetPlaylistsAsync();
    Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit);
    Task<Track?> GetTrackAsync(string trackId);
    Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids);
    Task<string> GetAudioFilePathAsync(string trackId, string destinationPath);
    Task<string?> GetCoverUrlAsync(string trackId);
    Task<IEnumerable<Track>> SearchAsync(string query);
}
