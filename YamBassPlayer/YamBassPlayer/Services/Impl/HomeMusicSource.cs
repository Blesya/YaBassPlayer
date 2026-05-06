using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Music source that exposes locally scanned audio files via <see cref="ILocalLibraryService"/>.
/// Metadata is stored in the SQLite database and refreshed by scanning folders.
/// </summary>
public sealed class LocalMusicSource : IMusicSource
{
    private readonly ILocalLibraryService _localLibraryService;

    public LocalMusicSource(ILocalLibraryService localLibraryService)
    {
        ArgumentNullException.ThrowIfNull(localLibraryService);
        _localLibraryService = localLibraryService;
    }

    public string SourceId => SourceIds.Local;
    public string DisplayName => "Локальная музыка";
    public bool SupportsSearch => true;
    public bool SupportsFavorites => false;

    /// <summary>
    /// Returns one <see cref="Playlist"/> per registered local folder
    /// (<see cref="PlaylistType.LocalFolder"/>) plus a single "Вся локальная музыка" playlist
    /// (<see cref="PlaylistType.LocalSearch"/>) when at least one folder is registered.
    /// The folder id is encoded in <see cref="Playlist.Description"/> so it can be decoded in
    /// <see cref="GetPlaylistTracksAsync"/>.
    /// </summary>
    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var folders = await _localLibraryService.GetFoldersAsync();

        var result = new List<Playlist>();
        foreach (var folder in folders)
        {
            var folderTracks = await _localLibraryService.GetTracksAsync(folder.Id);
            result.Add(new Playlist(folder.Name, PlaylistType.LocalFolder)
            {
                // Encode the folder id in Description so GetPlaylistTracksAsync can route correctly.
                Description = folder.Id.ToString(),
                TrackCount = folderTracks.Count,
            });
        }

        if (result.Count > 0)
        {
            var allTracks = await _localLibraryService.GetTracksAsync(null);
            result.Add(new Playlist("Вся локальная музыка", PlaylistType.LocalSearch)
            {
                TrackCount = allTracks.Count,
                Description = string.Empty,
            });
        }

        return result;
    }

    /// <summary>
    /// Returns only track IDs (file paths) for the given local playlist.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetPlaylistTrackIdsAsync(Playlist playlist, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tracks = await GetPlaylistTracksAsync(playlist, 0, int.MaxValue, ct);
        return tracks.Select(t => t.Id).ToList();
    }

    /// <summary>
    /// Returns a paginated slice of tracks for the given playlist.
    /// For <see cref="PlaylistType.LocalFolder"/>, the folder id is decoded from
    /// <see cref="Playlist.Description"/>. For <see cref="PlaylistType.LocalSearch"/>,
    /// all local tracks are returned. Offset and limit are applied in memory.
    /// </summary>
    public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Track> tracks = playlist.Type switch
        {
            PlaylistType.LocalFolder when int.TryParse(playlist.Description, out int folderId)
                => await _localLibraryService.GetTracksAsync(folderId),
            PlaylistType.LocalSearch
                => await _localLibraryService.GetTracksAsync(null),
            _ => [],
        };

        return tracks.Skip(offset).Take(limit);
    }

    /// <summary>
    /// Looks up a single track by its file path (the track ID for local sources).
    /// Returns <see langword="null"/> if the file no longer exists.
    /// </summary>
    public Task<Track?> GetTrackAsync(string trackId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(trackId))
            return Task.FromResult<Track?>(null);

        return Task.FromResult<Track?>(_localLibraryService.ParseTrackFromFile(trackId));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tasks = ids.Select(id => GetTrackAsync(id, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(t => t is not null).Select(t => t!);
    }

    /// <summary>
    /// For local sources the track ID is the absolute file path, so no copying is needed.
    /// <paramref name="destinationPath"/> is ignored.
    /// </summary>
    public Task<string> GetAudioFilePathAsync(string trackId, string destinationPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(trackId))
            throw new FileNotFoundException("Local audio file not found.", trackId);

        return Task.FromResult(trackId);
    }

    /// <summary>
    /// Cover art extraction from embedded ID3 tags is not implemented yet.
    /// TODO: Use TagLib# to read embedded cover art and return a temp-file URL or data URI.
    /// </summary>
    public Task<string?> GetCoverUrlAsync(string trackId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Delegates full-text search (title, artist, album) to <see cref="ILocalLibraryService"/>.
    /// </summary>
    public async Task<IEnumerable<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _localLibraryService.SearchTracksAsync(query);
    }
}
