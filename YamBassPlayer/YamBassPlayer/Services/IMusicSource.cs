using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IMusicSource
{
    string SourceId { get; }
    string DisplayName { get; }
    bool SupportsSearch { get; }
    bool SupportsFavorites { get; }

    Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default);
    Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPlaylistTrackIdsAsync(Playlist playlist, CancellationToken ct = default);
    Task<Track?> GetTrackAsync(string trackId, CancellationToken ct = default);
    Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<string> GetAudioFilePathAsync(string trackId, string destinationPath, CancellationToken ct = default);
    Task<string?> GetCoverUrlAsync(string trackId, CancellationToken ct = default);
    Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default);
}
