using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Фасад над локальной библиотекой: делегирует чтение/запись в
/// <see cref="LocalLibraryRepository"/>, а сканирование — в <see cref="LocalLibraryScanner"/>.
/// Сохраняет границы блокировки записи: <see cref="AddFolderAsync"/>, <see cref="RemoveFolderAsync"/>
/// и <see cref="ScanAllFoldersAsync"/> удерживают блокировку на время всей операции,
/// а <see cref="ScanFolderAsync"/> её не захватывает (его вызывают из-под блокировки).
/// </summary>
public sealed class LocalLibraryService : ILocalLibraryService
{
	private readonly LocalLibraryRepository _repository;
	private readonly LocalLibraryScanner _scanner;
	private readonly IDbWriteLock _writeLock;

	public LocalLibraryService(LocalLibraryRepository repository, LocalLibraryScanner scanner, IDbWriteLock writeLock)
	{
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(scanner);
		ArgumentNullException.ThrowIfNull(writeLock);
		_repository = repository;
		_scanner = scanner;
		_writeLock = writeLock;
	}

	// События живут в сканере; фасад лишь перенаправляет подписки, сохраняя контракт.
	public event Action<string>? OnScanProgress
	{
		add => _scanner.OnScanProgress += value;
		remove => _scanner.OnScanProgress -= value;
	}

	public event Action<int>? OnScanCompleted
	{
		add => _scanner.OnScanCompleted += value;
		remove => _scanner.OnScanCompleted -= value;
	}

	/// <summary>Returns all registered local folders ordered by name.</summary>
	public Task<IReadOnlyList<LocalFolder>> GetFoldersAsync() => _repository.GetFoldersAsync();

	/// <summary>
	/// Registers a new folder path, validates it exists on disk, and immediately scans it
	/// for audio files. If the path is already registered, returns the existing folder after
	/// scanning.
	/// </summary>
	/// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="path"/> does not exist.</exception>
	public async Task<LocalFolder> AddFolderAsync(string path)
	{
		using var writeLock = await _writeLock.AcquireAsync();

		LocalFolder folder = await _repository.AddFolderAsync(path);
		await _scanner.ScanFolderAsync(folder.Id);
		return folder;
	}

	/// <summary>
	/// Removes a folder and all its associated local tracks (including artist links) within a
	/// single transaction to prevent orphaned rows.
	/// </summary>
	public async Task RemoveFolderAsync(int folderId)
	{
		using var writeLock = await _writeLock.AcquireAsync();
		await _repository.RemoveFolderAsync(folderId);
	}

	/// <summary>
	/// Recursively scans a registered folder for audio files, reads ID3 tags via TagLib#,
	/// and upserts track records into the database. Progress is reported per file name.
	/// </summary>
	/// <returns>Number of tracks found or updated.</returns>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="folderId"/> is not found.</exception>
	public Task<int> ScanFolderAsync(int folderId, IProgress<string>? progress = null) =>
		_scanner.ScanFolderAsync(folderId, progress);

	/// <summary>
	/// Scans all registered folders sequentially and returns the total track count.
	/// </summary>
	public async Task<int> ScanAllFoldersAsync(IProgress<string>? progress = null)
	{
		using var writeLock = await _writeLock.AcquireAsync();

		IReadOnlyList<LocalFolder> folders = await _repository.GetFoldersAsync();
		int total = 0;
		foreach (LocalFolder folder in folders)
			total += await _scanner.ScanFolderAsync(folder.Id, progress);
		return total;
	}

	/// <summary>
	/// Returns local tracks optionally filtered by <paramref name="folderId"/>,
	/// ordered by Artist → Album → Title.
	/// </summary>
	public Task<IReadOnlyList<Track>> GetTracksAsync(int? folderId = null) =>
		_repository.GetTracksAsync(folderId);

	/// <summary>
	/// Returns the number of local tracks, optionally filtered by <paramref name="folderId"/>.
	/// Cheaper than loading full track rows when only a count is needed.
	/// </summary>
	public Task<int> GetTrackCountAsync(int? folderId = null) =>
		_repository.GetTrackCountAsync(folderId);

	/// <summary>
	/// Searches local tracks by title, artist, or album (case-insensitive LIKE substring match).
	/// Returns at most 100 results ordered by Artist, Title.
	/// </summary>
	public Task<IReadOnlyList<Track>> SearchTracksAsync(string query) =>
		_repository.SearchTracksAsync(query);

	/// <summary>
	/// Builds a <see cref="Track"/> from a local audio file using TagLib# ID3 tag reading.
	/// Falls back to filename heuristics when tags are unavailable or the file is corrupt.
	/// </summary>
	public Track ParseTrackFromFile(string filePath) => _scanner.ParseTrackFromFile(filePath);

	/// <summary>
	/// Returns all distinct artists in the local library with their track counts.
	/// Tracks stored without an artist tag are surfaced as "Неизвестный исполнитель".
	/// </summary>
	public Task<IReadOnlyList<(string artistName, int trackCount)>> GetLocalArtistsAsync(int? folderId = null) =>
		_repository.GetLocalArtistsAsync(folderId);

	/// <summary>
	/// Returns all local tracks for the given artist, ordered by album then title.
	/// Passing "Неизвестный исполнитель" returns tracks with a null or empty artist tag.
	/// </summary>
	public Task<IReadOnlyList<Track>> GetTracksByArtistAsync(string artistName, int? folderId = null) =>
		_repository.GetTracksByArtistAsync(artistName, folderId);

	/// <summary>
	/// Returns all distinct albums for the given artist in the local library, with track counts.
	/// Tracks stored without an album tag are surfaced as "Без альбома".
	/// Pass "Неизвестный исполнитель" to query tracks with no artist tag.
	/// </summary>
	public Task<IReadOnlyList<(string albumName, int trackCount)>> GetLocalAlbumsAsync(string artistName, int? folderId = null) =>
		_repository.GetLocalAlbumsAsync(artistName, folderId);

	/// <summary>
	/// Returns all local tracks for the given artist and album, ordered by title.
	/// Pass "Неизвестный исполнитель" for tracks with no artist tag, "Без альбома" for no album tag.
	/// </summary>
	public Task<IReadOnlyList<Track>> GetTracksByAlbumAsync(string artistName, string albumName, int? folderId = null) =>
		_repository.GetTracksByAlbumAsync(artistName, albumName, folderId);

	/// <summary>
	/// Returns all distinct album titles across every artist in the local library, with track counts.
	/// Tracks stored without an album tag are surfaced as "Без альбома".
	/// </summary>
	public Task<IReadOnlyList<(string albumName, int trackCount)>> GetAllLocalAlbumsAsync(int? folderId = null) =>
		_repository.GetAllLocalAlbumsAsync(folderId);

	/// <summary>
	/// Returns all local tracks whose album title matches <paramref name="albumName"/>, regardless of artist,
	/// ordered by artist then title.
	/// Pass "Без альбома" to get tracks with no album tag.
	/// </summary>
	public Task<IReadOnlyList<Track>> GetTracksByAlbumTitleAsync(string albumName, int? folderId = null) =>
		_repository.GetTracksByAlbumTitleAsync(albumName, folderId);
}
