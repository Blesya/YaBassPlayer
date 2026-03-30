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

    public string SourceId => "local";
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
    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync()
    {
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
    /// Returns a paginated slice of tracks for the given playlist.
    /// For <see cref="PlaylistType.LocalFolder"/>, the folder id is decoded from
    /// <see cref="Playlist.Description"/>. For <see cref="PlaylistType.LocalSearch"/>,
    /// all local tracks are returned. Offset and limit are applied in memory.
    /// </summary>
    public async Task<IEnumerable<Track>> GetPlaylistTracksAsync(Playlist playlist, int offset, int limit)
    {
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
    public Task<Track?> GetTrackAsync(string trackId)
    {
        if (!File.Exists(trackId))
            return Task.FromResult<Track?>(null);

        return Task.FromResult<Track?>(ParseTrackFromFile(trackId));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Track>> GetTracksByIdsAsync(IEnumerable<string> ids)
    {
        var tasks = ids.Select(GetTrackAsync);
        var results = await Task.WhenAll(tasks);
        return results.Where(t => t is not null).Select(t => t!);
    }

    /// <summary>
    /// For local sources the track ID is the absolute file path, so no copying is needed.
    /// <paramref name="destinationPath"/> is ignored.
    /// </summary>
    public Task<string> GetAudioFilePathAsync(string trackId, string destinationPath)
    {
        if (!File.Exists(trackId))
            throw new FileNotFoundException("Local audio file not found.", trackId);

        return Task.FromResult(trackId);
    }

    /// <summary>
    /// Cover art extraction from embedded ID3 tags is not implemented yet.
    /// TODO: Use TagLib# to read embedded cover art and return a temp-file URL or data URI.
    /// </summary>
    public Task<string?> GetCoverUrlAsync(string trackId) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Delegates full-text search (title, artist, album) to <see cref="ILocalLibraryService"/>.
    /// </summary>
    public async Task<IEnumerable<Track>> SearchAsync(string query)
        => await _localLibraryService.SearchTracksAsync(query);

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="Track"/> from a local audio file path using TagLib# ID3 tag reading.
    /// Falls back to filename heuristics when tags are unavailable or the file is corrupt.
    /// </summary>
    private static Track ParseTrackFromFile(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var tag = tagFile.Tag;

            string title = !string.IsNullOrWhiteSpace(tag.Title)
                ? tag.Title
                : Path.GetFileNameWithoutExtension(filePath);

            string artist = tag.Performers.Length > 0
                ? string.Join(", ", tag.Performers)
                : "Неизвестный исполнитель";

            string album = !string.IsNullOrWhiteSpace(tag.Album)
                ? tag.Album
                : Path.GetDirectoryName(filePath) is { } dir ? Path.GetFileName(dir) : "";

            var artists = tag.Performers.Length > 0
                ? tag.Performers.Select(p => new Artist(p, p)).ToList()
                : null;

            var albumInfo = !string.IsNullOrWhiteSpace(tag.Album)
                ? new Album(tag.Album, tag.Album)
                {
                    Year = tag.Year > 0 ? (int?)tag.Year : null,
                    Genre = tag.Genres.Length > 0 ? tag.Genres[0] : null,
                }
                : null;

            IReadOnlyList<string>? genres = tag.Genres.Length > 0
                ? tag.Genres.ToList()
                : null;

            long? durationMs = tagFile.Properties?.Duration is { } dur && dur > TimeSpan.Zero
                ? (long?)dur.TotalMilliseconds
                : null;

            return new Track(title, artist, album, filePath)
            {
                SourceType = "local",
                Artists = artists,
                AlbumInfo = albumInfo,
                Year = tag.Year > 0 ? (int?)tag.Year : null,
                Genres = genres,
                DurationMs = durationMs,
            };
        }
        catch
        {
            // Fallback to filename parsing if TagLib fails (corrupt file, unsupported format)
            return ParseTrackFromFilename(filePath);
        }
    }

    /// <summary>
    /// Builds a <see cref="Track"/> from a local audio file path using filename heuristics.
    /// Used as a fallback when TagLib# cannot read the file.
    /// </summary>
    /// <remarks>
    /// Supported filename pattern: <c>Artist - Title.ext</c> (split on first " - ").
    /// When the pattern is not matched, the full filename (without extension) becomes the title
    /// and the artist falls back to "Неизвестный исполнитель".
    /// The parent directory name is used as the album.
    /// </remarks>
    private static Track ParseTrackFromFilename(string filePath)
    {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        string title, artist;

        // Attempt "Artist - Title" pattern; require content on both sides of the separator.
        int separatorIdx = filename.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIdx > 0)
        {
            artist = filename[..separatorIdx].Trim();
            title = filename[(separatorIdx + 3)..].Trim();
        }
        else
        {
            title = filename;
            artist = "Неизвестный исполнитель";
        }

        string album = Path.GetDirectoryName(filePath) is { } dir ? Path.GetFileName(dir) : "";
        return new Track(title, artist, album, filePath) { SourceType = "local" };
    }
}
