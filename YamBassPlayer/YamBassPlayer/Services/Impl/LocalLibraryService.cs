using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Manages locally scanned music folders and their tracks in the SQLite database.
/// Audio metadata is read from ID3 tags via TagLib#, with a filename-based fallback for
/// corrupt or unsupported files.
/// </summary>
public sealed class LocalLibraryService : ILocalLibraryService
{
	private const string TrackProjection =
		"TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, COALESCE(SourceTrackId, TrackId), LocalFilePath";

	private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp3", ".flac", ".ogg", ".wav", ".m4a"
	};

	private readonly SqliteConnection _connection;
	private readonly string _coversFolder;

	public event Action<string>? OnScanProgress;
	public event Action<int>? OnScanCompleted;

	public LocalLibraryService(SqliteConnection connection, string coversFolder)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(coversFolder);
		_connection = connection;
		_coversFolder = coversFolder;

		if (!Directory.Exists(_coversFolder))
			Directory.CreateDirectory(_coversFolder);

		EnsureTrackCoverColumn("RemoteCoverUrl", "TEXT");
		EnsureTrackCoverColumn("LocalCoverPath", "TEXT");
		BackfillTrackCoverMetadataColumns();
	}

	/// <summary>Returns all registered local folders ordered by name.</summary>
	public async Task<IReadOnlyList<LocalFolder>> GetFoldersAsync()
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT Id, Path, Name, AddedAt, LastScannedAt FROM LocalFolders ORDER BY Name";

		var folders = new List<LocalFolder>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			folders.Add(ReadLocalFolder(reader));

		return folders;
	}

	/// <summary>
	/// Registers a new folder path, validates it exists on disk, and immediately scans it
	/// for audio files. If the path is already registered, returns the existing folder after
	/// scanning.
	/// </summary>
	/// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="path"/> does not exist.</exception>
	public async Task<LocalFolder> AddFolderAsync(string path)
	{
		if (!Directory.Exists(path))
			throw new DirectoryNotFoundException($"Directory not found: {path}");

		// Trim trailing separators so GetFileName works correctly on "C:\Music\" etc.
		string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string name = Path.GetFileName(trimmed);
		if (string.IsNullOrEmpty(name))
			name = path; // root path (e.g. "C:\")

		long addedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = "INSERT OR IGNORE INTO LocalFolders (Path, Name, AddedAt) VALUES (@path, @name, @addedAt)";
			cmd.Parameters.AddWithValue("@path", path);
			cmd.Parameters.AddWithValue("@name", name);
			cmd.Parameters.AddWithValue("@addedAt", addedAt);
			await cmd.ExecuteNonQueryAsync();
		}

		// Fetch the row (inserted or already-existing)
		LocalFolder folder;
		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = "SELECT Id, Path, Name, AddedAt, LastScannedAt FROM LocalFolders WHERE Path = @path";
			cmd.Parameters.AddWithValue("@path", path);
			using var reader = await cmd.ExecuteReaderAsync();
			if (!await reader.ReadAsync())
				throw new InvalidOperationException($"Failed to retrieve folder row after insert: {path}");
			folder = ReadLocalFolder(reader);
		}

		await ScanFolderAsync(folder.Id);
		return folder;
	}

	/// <summary>
	/// Removes a folder and all its associated local tracks (including artist links) within a
	/// single transaction to prevent orphaned rows.
	/// </summary>
	public async Task RemoveFolderAsync(int folderId)
	{
		using var transaction = _connection.BeginTransaction();

		// Delete artist links first while Tracks rows still exist for the subquery.
		using (var cmd = _connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = @"
				DELETE FROM TrackArtists
				WHERE TrackId IN (
					SELECT TrackId FROM Tracks WHERE FolderId = @folderId AND SourceType = 'local'
				)";
			cmd.Parameters.AddWithValue("@folderId", folderId);
			await cmd.ExecuteNonQueryAsync();
		}

		using (var cmd = _connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = "DELETE FROM Tracks WHERE FolderId = @folderId AND SourceType = 'local'";
			cmd.Parameters.AddWithValue("@folderId", folderId);
			await cmd.ExecuteNonQueryAsync();
		}

		using (var cmd = _connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = "DELETE FROM LocalFolders WHERE Id = @folderId";
			cmd.Parameters.AddWithValue("@folderId", folderId);
			await cmd.ExecuteNonQueryAsync();
		}

		transaction.Commit();
	}

	/// <summary>
	/// Recursively scans a registered folder for audio files, reads ID3 tags via TagLib#,
	/// and upserts track records into the database. Progress is reported per file name.
	/// </summary>
	/// <returns>Number of tracks found or updated.</returns>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="folderId"/> is not found.</exception>
	public async Task<int> ScanFolderAsync(int folderId, IProgress<string>? progress = null)
	{
		string? folderPath;
		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = "SELECT Path FROM LocalFolders WHERE Id = @id";
			cmd.Parameters.AddWithValue("@id", folderId);
			folderPath = (string?)await cmd.ExecuteScalarAsync();
		}

		if (folderPath is null)
			throw new InvalidOperationException($"Folder with id {folderId} not found.");

		if (!Directory.Exists(folderPath))
		{
			await RemoveMissingLocalTracksAsync(folderId, []);
			await UpdateFolderLastScannedAtAsync(folderId);
			OnScanCompleted?.Invoke(0);
			return 0;
		}

		List<string> audioFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
			.Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
			.ToList();

		long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		int count = 0;

		foreach (string filePath in audioFiles)
		{
			string fileName = Path.GetFileName(filePath);
			progress?.Report(fileName);
			OnScanProgress?.Invoke(fileName);

			Track track = ParseTrackFromFile(filePath);
			await SaveLocalTrackAsync(track, folderId, updatedAt);
			count++;
		}

		await RemoveMissingLocalTracksAsync(folderId, audioFiles);
		await UpdateFolderLastScannedAtAsync(folderId);

		OnScanCompleted?.Invoke(count);
		return count;
	}

	/// <summary>Scans all registered folders sequentially and returns the total track count.</summary>
	public async Task<int> ScanAllFoldersAsync(IProgress<string>? progress = null)
	{
		IReadOnlyList<LocalFolder> folders = await GetFoldersAsync();
		int total = 0;
		foreach (LocalFolder folder in folders)
			total += await ScanFolderAsync(folder.Id, progress);
		return total;
	}

	/// <summary>
	/// Returns local tracks optionally filtered by <paramref name="folderId"/>,
	/// ordered by Artist → Album → Title.
	/// </summary>
	public async Task<IReadOnlyList<Track>> GetTracksAsync(int? folderId = null)
	{
		using var cmd = _connection.CreateCommand();

		if (folderId.HasValue)
		{
			cmd.CommandText = @"
				SELECT " + TrackProjection + @"
				FROM Tracks
				WHERE SourceType = 'local' AND FolderId = @folderId
				ORDER BY Artist, Album, Title";
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);
		}
		else
		{
			cmd.CommandText = @"
				SELECT " + TrackProjection + @"
				FROM Tracks
				WHERE SourceType = 'local'
				ORDER BY Artist, Album, Title";
		}

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(ReadTrack(reader));

		return tracks;
	}

	/// <summary>
	/// Searches local tracks by title, artist, or album (case-insensitive LIKE substring match).
	/// Returns at most 100 results ordered by Artist, Title.
	/// </summary>
	public async Task<IReadOnlyList<Track>> SearchTracksAsync(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = @"
			SELECT " + TrackProjection + @"
			FROM Tracks
			WHERE SourceType = 'local'
			  AND (Title LIKE @q OR Artist LIKE @q OR Album LIKE @q)
			ORDER BY Artist, Title
			LIMIT 100";
		cmd.Parameters.AddWithValue("@q", $"%{query}%");

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(ReadTrack(reader));

		return tracks;
	}

	// -------------------------------------------------------------------------
	// Artist queries
	// -------------------------------------------------------------------------

	/// <summary>
	/// Returns all distinct artists in the local library with their track counts.
	/// Tracks stored without an artist tag are surfaced as "Неизвестный исполнитель".
	/// </summary>
	public async Task<IReadOnlyList<(string artistName, int trackCount)>> GetLocalArtistsAsync(int? folderId = null)
	{
		using var cmd = _connection.CreateCommand();

		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT
				CASE WHEN Artist IS NULL OR Artist = '' THEN 'Неизвестный исполнитель' ELSE Artist END AS ArtistName,
				COUNT(*) AS TrackCount
			FROM Tracks
			WHERE SourceType = 'local'{folderFilter}
			GROUP BY ArtistName
			ORDER BY ArtistName ASC";

		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var result = new List<(string, int)>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add((reader.GetString(0), reader.GetInt32(1)));

		return result;
	}

	/// <summary>
	/// Returns all local tracks for the given artist, ordered by album then title.
	/// Passing "Неизвестный исполнитель" returns tracks with a null or empty artist tag.
	/// </summary>
	public async Task<IReadOnlyList<Track>> GetTracksByArtistAsync(string artistName, int? folderId = null)
	{
		ArgumentNullException.ThrowIfNull(artistName);

		using var cmd = _connection.CreateCommand();

		bool isUnknown = artistName == "Неизвестный исполнитель";
		string artistFilter = isUnknown
			? "(Artist IS NULL OR Artist = '')"
			: "Artist = @artist";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";

		cmd.CommandText = $@"
			SELECT {TrackProjection}
			FROM Tracks
			WHERE SourceType = 'local' AND {artistFilter}{folderFilter}
			ORDER BY Album, Title";

		if (!isUnknown)
			cmd.Parameters.AddWithValue("@artist", artistName);
		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(ReadTrack(reader));

		return tracks;
	}

	/// <summary>
	/// Returns all distinct albums for the given artist in the local library, with track counts.
	/// Tracks stored without an album tag are surfaced as "Без альбома".
	/// Pass "Неизвестный исполнитель" to query tracks with no artist tag.
	/// </summary>
	public async Task<IReadOnlyList<(string albumName, int trackCount)>> GetLocalAlbumsAsync(string artistName, int? folderId = null)
	{
		ArgumentNullException.ThrowIfNull(artistName);

		using var cmd = _connection.CreateCommand();
		bool isUnknown = artistName == "Неизвестный исполнитель";
		string artistFilter = isUnknown ? "(Artist IS NULL OR Artist = '')" : "Artist = @artist";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT COALESCE(NULLIF(Album, ''), 'Без альбома') AS AlbumName, COUNT(*) AS TrackCount
			FROM Tracks
			WHERE SourceType = 'local' AND {artistFilter}{folderFilter}
			GROUP BY AlbumName
			ORDER BY AlbumName";

		if (!isUnknown)
			cmd.Parameters.AddWithValue("@artist", artistName);
		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var result = new List<(string, int)>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add((reader.GetString(0), reader.GetInt32(1)));

		return result;
	}

	/// <summary>
	/// Returns all local tracks for the given artist and album, ordered by title.
	/// Pass "Неизвестный исполнитель" for tracks with no artist tag, "Без альбома" for no album tag.
	/// </summary>
	public async Task<IReadOnlyList<Track>> GetTracksByAlbumAsync(string artistName, string albumName, int? folderId = null)
	{
		ArgumentNullException.ThrowIfNull(artistName);
		ArgumentNullException.ThrowIfNull(albumName);

		using var cmd = _connection.CreateCommand();
		bool isUnknownArtist = artistName == "Неизвестный исполнитель";
		bool isUnknownAlbum = albumName == "Без альбома";
		string artistFilter = isUnknownArtist ? "(Artist IS NULL OR Artist = '')" : "Artist = @artist";
		string albumFilter = isUnknownAlbum ? "(Album IS NULL OR Album = '')" : "Album = @album";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT {TrackProjection}
			FROM Tracks
			WHERE SourceType = 'local' AND {artistFilter} AND {albumFilter}{folderFilter}
			ORDER BY Title";

		if (!isUnknownArtist)
			cmd.Parameters.AddWithValue("@artist", artistName);
		if (!isUnknownAlbum)
			cmd.Parameters.AddWithValue("@album", albumName);
		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(ReadTrack(reader));

		return tracks;
	}

	/// <summary>
	/// Returns all distinct album titles across every artist in the local library, with track counts.
	/// Tracks stored without an album tag are surfaced as "Без альбома".
	/// </summary>
	public async Task<IReadOnlyList<(string albumName, int trackCount)>> GetAllLocalAlbumsAsync(int? folderId = null)
	{
		using var cmd = _connection.CreateCommand();
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT COALESCE(NULLIF(Album, ''), 'Без альбома') AS AlbumName, COUNT(*) AS TrackCount
			FROM Tracks
			WHERE SourceType = 'local'{folderFilter}
			GROUP BY AlbumName
			ORDER BY AlbumName";

		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var result = new List<(string, int)>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			result.Add((reader.GetString(0), reader.GetInt32(1)));

		return result;
	}

	/// <summary>
	/// Returns all local tracks whose album title matches <paramref name="albumName"/>, regardless of artist,
	/// ordered by artist then title.
	/// Pass "Без альбома" to get tracks with no album tag.
	/// </summary>
	public async Task<IReadOnlyList<Track>> GetTracksByAlbumTitleAsync(string albumName, int? folderId = null)
	{
		ArgumentNullException.ThrowIfNull(albumName);

		using var cmd = _connection.CreateCommand();
		bool isUnknown = albumName == "Без альбома";
		string albumFilter = isUnknown ? "(Album IS NULL OR Album = '')" : "Album = @album";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT {TrackProjection}
			FROM Tracks
			WHERE SourceType = 'local' AND {albumFilter}{folderFilter}
			ORDER BY Artist, Title";

		if (!isUnknown)
			cmd.Parameters.AddWithValue("@album", albumName);
		if (folderId.HasValue)
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);

		var tracks = new List<Track>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			tracks.Add(ReadTrack(reader));

		return tracks;
	}

	// -------------------------------------------------------------------------
	// Private helpers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Builds a <see cref="Track"/> from a local audio file using TagLib# ID3 tag reading.
	/// Falls back to filename heuristics when tags are unavailable or the file is corrupt.
	/// </summary>
	private Track ParseTrackFromFile(string filePath)
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

			string? localCoverPath = ExtractCoverArt(filePath, tagFile);

			return new Track(title, artist, album, filePath)
			{
				SourceType = "local",
				SourceTrackId = filePath,
				LocalFilePath = filePath,
				Artists = artists,
				AlbumInfo = albumInfo,
				Year = tag.Year > 0 ? (int?)tag.Year : null,
				Genres = genres,
				DurationMs = durationMs,
				CoverUrl = localCoverPath,
				LocalCoverPath = localCoverPath,
			};
		}
		catch
		{
			// Fallback to filename parsing if TagLib fails (corrupt file, unsupported format).
			// Still attempt to find a cover image in the folder.
			return ParseTrackFromFilename(filePath, FindFolderCoverArt(filePath));
		}
	}

	/// <summary>
	/// Builds a <see cref="Track"/> from a local audio file path using filename heuristics.
	/// Supported pattern: <c>Artist - Title.ext</c> (split on first " - ").
	/// </summary>
	private static Track ParseTrackFromFilename(string filePath, string? coverUrl = null)
	{
		string filename = Path.GetFileNameWithoutExtension(filePath);
		string title, artist;

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
		return new Track(title, artist, album, filePath)
		{
			SourceType = "local",
			SourceTrackId = filePath,
			LocalFilePath = filePath,
			CoverUrl = coverUrl,
			LocalCoverPath = coverUrl,
		};
	}

	/// <summary>
	/// Upserts a local track into Tracks, Artists, and TrackArtists tables.
	/// This step keeps TrackId compatible with existing playback while also persisting
	/// explicit source-aware fields for future storage work.
	/// </summary>
	private async Task SaveLocalTrackAsync(Track track, int folderId, long updatedAt)
	{
		string? genresJson = track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null;
		string localFilePath = track.LocalFilePath ?? track.Id;
		string? localCoverPath = ResolveLocalCoverPath(track.SourceType, track.CoverUrl, track.LocalCoverPath);
		string? coverUrl = ResolveLegacyCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl, localCoverPath);

		using var transaction = _connection.BeginTransaction();

		using (var cmd = _connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = @"
				INSERT OR REPLACE INTO Tracks
					(TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, SourceTrackId, LocalFilePath, FolderId, UpdatedAt)
				VALUES
					(@trackId, @artist, @title, @album, @durationMs, @year, @coverUrl, @remoteCoverUrl, @localCoverPath, @genres, @albumId, 'local', @sourceTrackId, @localFilePath, @folderId, @updatedAt)";
			cmd.Parameters.AddWithValue("@trackId", track.Id);
			cmd.Parameters.AddWithValue("@artist", track.Artist);
			cmd.Parameters.AddWithValue("@title", track.Title);
			cmd.Parameters.AddWithValue("@album", track.Album);
			cmd.Parameters.AddWithValue("@durationMs", (object?)track.DurationMs ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@year", (object?)track.Year ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@coverUrl", (object?)coverUrl ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@remoteCoverUrl", DBNull.Value);
			cmd.Parameters.AddWithValue("@localCoverPath", (object?)localCoverPath ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@genres", (object?)genresJson ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@albumId", (object?)track.AlbumInfo?.Id ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@sourceTrackId", track.SourceTrackId);
			cmd.Parameters.AddWithValue("@localFilePath", localFilePath);
			cmd.Parameters.AddWithValue("@folderId", folderId);
			cmd.Parameters.AddWithValue("@updatedAt", updatedAt);
			await cmd.ExecuteNonQueryAsync();
		}

		using (var clearArtistLinksCmd = _connection.CreateCommand())
		{
			clearArtistLinksCmd.Transaction = transaction;
			clearArtistLinksCmd.CommandText = "DELETE FROM TrackArtists WHERE TrackId = @trackId";
			clearArtistLinksCmd.Parameters.AddWithValue("@trackId", track.Id);
			await clearArtistLinksCmd.ExecuteNonQueryAsync();
		}

		if (track.Artists is { Count: > 0 } artists)
		{
			foreach (Artist artist in artists)
			{
				// Local artists use their name as the ID since they have no external identifier.
				using (var artistCmd = _connection.CreateCommand())
				{
					artistCmd.Transaction = transaction;
					artistCmd.CommandText = @"
						INSERT OR REPLACE INTO Artists (Id, Name, CoverUrl, Description, UpdatedAt)
						VALUES (@id, @name, @coverUrl, @description, @updatedAt)";
					artistCmd.Parameters.AddWithValue("@id", artist.Id);
					artistCmd.Parameters.AddWithValue("@name", artist.Name);
					artistCmd.Parameters.AddWithValue("@coverUrl", DBNull.Value);
					artistCmd.Parameters.AddWithValue("@description", DBNull.Value);
					artistCmd.Parameters.AddWithValue("@updatedAt", updatedAt);
					await artistCmd.ExecuteNonQueryAsync();
				}

				using (var linkCmd = _connection.CreateCommand())
				{
					linkCmd.Transaction = transaction;
					linkCmd.CommandText = @"
						INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
						VALUES (@trackId, @artistId)";
					linkCmd.Parameters.AddWithValue("@trackId", track.Id);
					linkCmd.Parameters.AddWithValue("@artistId", artist.Id);
					await linkCmd.ExecuteNonQueryAsync();
				}
			}
		}

		transaction.Commit();
	}

	/// <summary>
	/// Tries to extract cover art for a track. First checks for embedded art in the ID3 tag;
	/// if none is found, falls back to well-known image filenames in the same directory.
	/// The extracted image is cached to <c>_coversFolder</c> using a path-derived hash.
	/// </summary>
	private string? ExtractCoverArt(string filePath, TagLib.File tagFile)
	{
		// Prefer embedded cover from ID3 tags.
		var picture = tagFile.Tag.Pictures?.FirstOrDefault();
		if (picture?.Data?.Data != null)
		{
			string coverFileName = Convert.ToHexString(
				MD5.HashData(System.Text.Encoding.UTF8.GetBytes(filePath))) + ".jpg";
			string coverPath = Path.Combine(_coversFolder, coverFileName);
			if (!File.Exists(coverPath))
				File.WriteAllBytes(coverPath, picture.Data.Data);
			return coverPath;
		}

		return FindFolderCoverArt(filePath);
	}

	/// <summary>
	/// Looks for a cover image file (cover.jpg, folder.jpg, etc.) in the same directory as
	/// <paramref name="filePath"/>. Returns the first match, or <see langword="null"/>.
	/// </summary>
	private static string? FindFolderCoverArt(string filePath)
	{
		string dir = Path.GetDirectoryName(filePath) ?? "";
		foreach (string name in new[] { "cover.jpg", "folder.jpg", "album.jpg", "cover.png", "folder.png" })
		{
			string candidate = Path.Combine(dir, name);
			if (File.Exists(candidate))
				return candidate;
		}
		return null;
	}

	private static Track ReadTrack(SqliteDataReader reader)
	{
		string trackId = reader.GetString(0);
		string artist = reader.IsDBNull(1) ? "" : reader.GetString(1);
		string title = reader.IsDBNull(2) ? "" : reader.GetString(2);
		string album = reader.IsDBNull(3) ? "" : reader.GetString(3);
		long? durationMs = reader.IsDBNull(4) ? null : reader.GetInt64(4);
		int? year = reader.IsDBNull(5) ? null : reader.GetInt32(5);
		string? coverUrl = reader.IsDBNull(6) ? null : reader.GetString(6);
		string? remoteCoverUrl = reader.IsDBNull(7) ? null : reader.GetString(7);
		string? localCoverPath = reader.IsDBNull(8) ? null : reader.GetString(8);
		string? genresJson = reader.IsDBNull(9) ? null : reader.GetString(9);
		string? albumId = reader.IsDBNull(10) ? null : reader.GetString(10);
		string sourceType = reader.IsDBNull(11) ? "local" : reader.GetString(11);
		string sourceTrackId = reader.IsDBNull(12) ? trackId : reader.GetString(12);
		string? localFilePath = reader.IsDBNull(13) ? null : reader.GetString(13);

		IReadOnlyList<string>? genres = null;
		if (genresJson is not null)
		{
			try { genres = JsonSerializer.Deserialize<List<string>>(genresJson); }
			catch { /* ignore malformed JSON — treat as no genres */ }
		}

		// Reconstruct a lightweight AlbumInfo from the denormalized Tracks columns.
		// Full enrichment (CoverUrl, Genre, TrackCount) is not loaded here to avoid N+1 queries.
		Album? albumInfo = albumId is not null ? new Album(albumId, album) { Year = year } : null;

		return new Track(title, artist, album, trackId)
		{
			DurationMs = durationMs,
			Year = year,
			CoverUrl = ResolveLegacyCoverUrl(sourceType, coverUrl, remoteCoverUrl, localCoverPath),
			RemoteCoverUrl = ResolveRemoteCoverUrl(sourceType, coverUrl, remoteCoverUrl),
			LocalCoverPath = ResolveLocalCoverPath(sourceType, coverUrl, localCoverPath),
			Genres = genres,
			SourceType = sourceType,
			SourceTrackId = sourceTrackId,
			LocalFilePath = localFilePath,
			AlbumInfo = albumInfo,
		};
	}

	private async Task RemoveMissingLocalTracksAsync(int folderId, IReadOnlyCollection<string> currentFilePaths)
	{
		var currentFilePathSet = new HashSet<string>(currentFilePaths, StringComparer.OrdinalIgnoreCase);
		var staleTrackIds = new List<string>();

		using (var cmd = _connection.CreateCommand())
		{
			cmd.CommandText = """
				SELECT TrackId, COALESCE(LocalFilePath, TrackId)
				FROM Tracks
				WHERE FolderId = @folderId AND SourceType = 'local'
				""";
			cmd.Parameters.AddWithValue("@folderId", folderId);

			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				string trackId = reader.GetString(0);
				string filePath = reader.GetString(1);
				if (!currentFilePathSet.Contains(filePath))
					staleTrackIds.Add(trackId);
			}
		}

		if (staleTrackIds.Count == 0)
			return;

		using var transaction = _connection.BeginTransaction();

		using (var deleteTrackArtistsCmd = _connection.CreateCommand())
		using (var deleteTracksCmd = _connection.CreateCommand())
		{
			deleteTrackArtistsCmd.Transaction = transaction;
			deleteTrackArtistsCmd.CommandText = "DELETE FROM TrackArtists WHERE TrackId = @trackId";
			deleteTrackArtistsCmd.Parameters.Add("@trackId", SqliteType.Text);

			deleteTracksCmd.Transaction = transaction;
			deleteTracksCmd.CommandText = "DELETE FROM Tracks WHERE TrackId = @trackId AND FolderId = @folderId AND SourceType = 'local'";
			deleteTracksCmd.Parameters.Add("@trackId", SqliteType.Text);
			deleteTracksCmd.Parameters.AddWithValue("@folderId", folderId);

			foreach (string staleTrackId in staleTrackIds)
			{
				deleteTrackArtistsCmd.Parameters["@trackId"].Value = staleTrackId;
				await deleteTrackArtistsCmd.ExecuteNonQueryAsync();

				deleteTracksCmd.Parameters["@trackId"].Value = staleTrackId;
				await deleteTracksCmd.ExecuteNonQueryAsync();
			}
		}

		transaction.Commit();
	}

	private void EnsureTrackCoverColumn(string columnName, string definition)
	{
		if (!HasTable("Tracks") || HasColumn("Tracks", columnName))
			return;

		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"ALTER TABLE Tracks ADD COLUMN {columnName} {definition};";
		cmd.ExecuteNonQuery();
	}

	private void BackfillTrackCoverMetadataColumns()
	{
		if (!HasTable("Tracks")
			|| !HasColumn("Tracks", "CoverUrl")
			|| !HasColumn("Tracks", "SourceType"))
			return;

		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			UPDATE Tracks
			SET RemoteCoverUrl = COALESCE(NULLIF(RemoteCoverUrl, ''), CoverUrl)
			WHERE COALESCE(SourceType, 'yandex') <> 'local'
			  AND CoverUrl IS NOT NULL
			  AND CoverUrl <> '';

			UPDATE Tracks
			SET LocalCoverPath = COALESCE(NULLIF(LocalCoverPath, ''), CoverUrl)
			WHERE COALESCE(SourceType, 'yandex') = 'local'
			  AND CoverUrl IS NOT NULL
			  AND CoverUrl <> '';
			""";
		cmd.ExecuteNonQuery();
	}

	private bool HasTable(string tableName)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName LIMIT 1;";
		cmd.Parameters.AddWithValue("@tableName", tableName);
		return cmd.ExecuteScalar() is not null;
	}

	private bool HasColumn(string tableName, string columnName)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = $"PRAGMA table_info({tableName});";
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			if (reader.GetString(1) == columnName)
				return true;
		}

		return false;
	}

	private static bool IsLocalSourceType(string sourceType)
		=> string.Equals(sourceType, "local", StringComparison.OrdinalIgnoreCase);

	private static string? ResolveRemoteCoverUrl(string sourceType, string? coverUrl, string? remoteCoverUrl)
	{
		if (!string.IsNullOrWhiteSpace(remoteCoverUrl))
			return remoteCoverUrl;

		return IsLocalSourceType(sourceType) ? null : coverUrl;
	}

	private static string? ResolveLocalCoverPath(string sourceType, string? coverUrl, string? localCoverPath)
	{
		if (!string.IsNullOrWhiteSpace(localCoverPath))
			return localCoverPath;

		return IsLocalSourceType(sourceType) ? coverUrl : null;
	}

	private static string? ResolveLegacyCoverUrl(
		string sourceType,
		string? coverUrl,
		string? remoteCoverUrl,
		string? localCoverPath)
	{
		if (!string.IsNullOrWhiteSpace(coverUrl))
			return coverUrl;

		return IsLocalSourceType(sourceType)
			? ResolveLocalCoverPath(sourceType, coverUrl, localCoverPath)
			: ResolveRemoteCoverUrl(sourceType, coverUrl, remoteCoverUrl);
	}

	private async Task UpdateFolderLastScannedAtAsync(int folderId)
	{
		long scannedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "UPDATE LocalFolders SET LastScannedAt = @scannedAt WHERE Id = @id";
		cmd.Parameters.AddWithValue("@scannedAt", scannedAt);
		cmd.Parameters.AddWithValue("@id", folderId);
		await cmd.ExecuteNonQueryAsync();
	}

	private static LocalFolder ReadLocalFolder(SqliteDataReader reader)
	{
		int id = reader.GetInt32(0);
		string path = reader.GetString(1);
		string name = reader.GetString(2);
		long addedAtSeconds = reader.GetInt64(3);
		long? lastScannedAtSeconds = reader.IsDBNull(4) ? null : reader.GetInt64(4);

		return new LocalFolder(id, path, name)
		{
			AddedAt = DateTimeOffset.FromUnixTimeSeconds(addedAtSeconds),
			LastScannedAt = lastScannedAtSeconds.HasValue
				? DateTimeOffset.FromUnixTimeSeconds(lastScannedAtSeconds.Value)
				: null,
		};
	}
}
