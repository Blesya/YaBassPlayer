using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ILocalLibraryService
{
	Task<IReadOnlyList<LocalFolder>> GetFoldersAsync();
	Task<LocalFolder> AddFolderAsync(string path);
	Task RemoveFolderAsync(int folderId);
	Task<int> ScanFolderAsync(int folderId, IProgress<string>? progress = null);
	Task<int> ScanAllFoldersAsync(IProgress<string>? progress = null);
	Task<IReadOnlyList<Track>> GetTracksAsync(int? folderId = null);
	Task<IReadOnlyList<Track>> SearchTracksAsync(string query);

	/// <summary>
	/// Returns all distinct artists found in the local library, with their track counts,
	/// ordered alphabetically. Tracks with no artist tag are grouped under "Неизвестный исполнитель".
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<(string artistName, int trackCount)>> GetLocalArtistsAsync(int? folderId = null);

	/// <summary>
	/// Returns all local tracks for the given artist name, ordered by album then title.
	/// Pass "Неизвестный исполнитель" to get tracks with no artist tag.
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<Track>> GetTracksByArtistAsync(string artistName, int? folderId = null);

	/// <summary>
	/// Returns all distinct albums for the given artist in the local library, with track counts,
	/// ordered alphabetically. Tracks with no album tag are grouped under "Без альбома".
	/// Pass "Неизвестный исполнитель" to query tracks with no artist tag.
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<(string albumName, int trackCount)>> GetLocalAlbumsAsync(string artistName, int? folderId = null);

	/// <summary>
	/// Returns all local tracks for the given artist and album, ordered by title.
	/// Pass "Неизвестный исполнитель" for tracks with no artist tag, "Без альбома" for no album tag.
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<Track>> GetTracksByAlbumAsync(string artistName, string albumName, int? folderId = null);

	/// <summary>
	/// Returns all distinct album titles across all artists in the local library, with track counts,
	/// ordered alphabetically. Tracks with no album tag are grouped under "Без альбома".
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<(string albumName, int trackCount)>> GetAllLocalAlbumsAsync(int? folderId = null);

	/// <summary>
	/// Returns all local tracks whose album title matches <paramref name="albumName"/>, regardless of artist,
	/// ordered by artist then title.
	/// Pass "Без альбома" to get tracks with no album tag.
	/// Optionally filtered to a single registered folder.
	/// </summary>
	Task<IReadOnlyList<Track>> GetTracksByAlbumTitleAsync(string albumName, int? folderId = null);

	event Action<string>? OnScanProgress;
	event Action<int>? OnScanCompleted;
}
