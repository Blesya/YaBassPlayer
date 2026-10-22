using System.Threading;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services;

public interface IMusicSource
{
    SourceId Id { get; }
    string DisplayName { get; }
    SourceState State { get; }
    bool SupportsSearch { get; }

    Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default);
    Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPlaylistTrackIdsAsync(Playlist playlist, CancellationToken ct = default);
    Task<Track?> GetTrackAsync(string trackId, CancellationToken ct = default);
    Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    /// <summary>
    /// Возвращает путь к играбельному файлу, скачивая его в <paramref name="destinationPath"/> при необходимости.
    /// Источник сам решает, как получить файл; локальный просто возвращает исходный путь.
    /// </summary>
    Task<string> GetAudioFilePathAsync(string trackId, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    Task<string?> GetCoverUrlAsync(string trackId, CancellationToken ct = default);
    Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default);
}
