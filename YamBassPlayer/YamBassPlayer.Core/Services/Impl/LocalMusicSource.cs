using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Music source that exposes locally scanned audio files via <see cref="ILocalLibraryService"/>.
/// Metadata is stored in the SQLite database and refreshed by scanning folders.
/// </summary>
public sealed class LocalMusicSource : IMusicSource
{
    public static readonly SourceId Id = new("local");

    private readonly ILocalLibraryService _localLibraryService;

    public LocalMusicSource(ILocalLibraryService localLibraryService)
    {
        ArgumentNullException.ThrowIfNull(localLibraryService);
        _localLibraryService = localLibraryService;
    }

    SourceId IMusicSource.Id => Id;
    public SourceState State => SourceState.Ready;
    public string DisplayName => "Локальная музыка";
    public bool SupportsSearch => true;

    /// <summary>
    /// Returns the local favorite playlist, one playlist per registered folder
    /// and a single "Вся локальная музыка" playlist when at least one folder is registered.
    /// </summary>
    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(CancellationToken ct = default)
    {
        Logging.LogBeforeCall();

        ct.ThrowIfCancellationRequested();
        var folders = await _localLibraryService.GetFoldersAsync();

        var result = new List<Playlist>();

        foreach (LocalFolder folder in folders)
        {
            int folderTrackCount = await _localLibraryService.GetTrackCountAsync(folder.Id);
            result.Add(new Playlist("folder", folder.Name)
            {
                SourceId = Id,
                Category = PlaylistCategory.Data,
                Description = folder.Path,
                TrackCount = folderTrackCount,
                Payload = new Dictionary<string, string> { ["folder-id"] = folder.Id.ToString() },
            });
        }

        if (folders.Count > 0)
        {
            int allTrackCount = await _localLibraryService.GetTrackCountAsync(null);
            result.Add(new Playlist("all", "Вся локальная музыка")
            {
                SourceId = Id,
                Category = PlaylistCategory.Data,
                Description = string.Empty,
                TrackCount = allTrackCount,
            });
        }

        Logging.LogAfterCall();
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
    /// </summary>
    public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Track> tracks = await LoadTracksAsync(playlist, ct);
        return tracks.Skip(offset).Take(limit);
    }

    private async Task<IReadOnlyList<Track>> LoadTracksAsync(Playlist playlist, CancellationToken ct)
    {
        switch (playlist.Kind)
        {
            case "folder" when TryGetFolderId(playlist, out int folderId):
                return await _localLibraryService.GetTracksAsync(folderId);
            case "all":
                return await _localLibraryService.GetTracksAsync(null);
            case "artist":
                return await _localLibraryService.GetTracksByArtistAsync(playlist.Title);
            case "album":
            {
                string artist = GetPayload(playlist, "artist");
                string? album = GetPayload(playlist, "album");
                if (string.IsNullOrEmpty(album))
                    return [];

                return string.IsNullOrEmpty(artist)
                    ? await _localLibraryService.GetTracksByAlbumTitleAsync(album)
                    : await _localLibraryService.GetTracksByAlbumAsync(artist, album);
            }
            default:
                return [];
        }
    }

    private static bool TryGetFolderId(Playlist playlist, out int folderId)
    {
        folderId = 0;
        return int.TryParse(GetPayload(playlist, "folder-id"), out folderId);
    }

    private static string GetPayload(Playlist playlist, string key)
        => playlist.Payload is { } payload && payload.TryGetValue(key, out string? value)
            ? value
            : string.Empty;

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
    public Task<string> GetAudioFilePathAsync(string trackId, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(trackId))
            throw new FileNotFoundException("Local audio file not found.", trackId);

        return Task.FromResult(trackId);
    }

    /// <summary>
    /// Cover art extraction from embedded ID3 tags is not implemented yet.
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
