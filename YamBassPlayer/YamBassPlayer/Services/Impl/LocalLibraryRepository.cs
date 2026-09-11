using System.Text.Json;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Доступ к таблицам локальной библиотеки (папки и треки): чтение, запись и запросы.
/// Не управляет блокировкой записи — её удерживает вызывающий код, чтобы охватить
/// составные операции (например, добавление папки вместе с её сканированием).
/// </summary>
public sealed class LocalLibraryRepository
{
	private const string TrackProjection =
		"TrackId, Artist, Title, Album, DurationMs, Year, TrackNumber, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, COALESCE(SourceTrackId, TrackId), LocalFilePath";

	private readonly IDbConnectionFactory _connectionFactory;

	public LocalLibraryRepository(IDbConnectionFactory connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		_connectionFactory = connectionFactory;
	}

	/// <summary>Returns all registered local folders ordered by name.</summary>
	public async Task<IReadOnlyList<LocalFolder>> GetFoldersAsync()
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT Id, Path, Name, AddedAt, LastScannedAt FROM LocalFolders ORDER BY Name";

		var folders = new List<LocalFolder>();
		using var reader = await cmd.ExecuteReaderAsync();
		while (await reader.ReadAsync())
			folders.Add(ReadLocalFolder(reader));

		return folders;
	}

	/// <summary>
	/// Registers a new folder path and validates it exists on disk. If the path is already
	/// registered, returns the existing folder. Scanning is left to the caller, which holds
	/// the write lock across the insert and the scan.
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

		LocalFolder folder;
		using (var connection = _connectionFactory.Create())
		{
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandText = "INSERT OR IGNORE INTO LocalFolders (Path, Name, AddedAt) VALUES (@path, @name, @addedAt)";
				cmd.Parameters.AddWithValue("@path", path);
				cmd.Parameters.AddWithValue("@name", name);
				cmd.Parameters.AddWithValue("@addedAt", addedAt);
				await cmd.ExecuteNonQueryAsync();
			}

			// Fetch the row (inserted or already-existing)
			using (var cmd = connection.CreateCommand())
			{
				cmd.CommandText = "SELECT Id, Path, Name, AddedAt, LastScannedAt FROM LocalFolders WHERE Path = @path";
				cmd.Parameters.AddWithValue("@path", path);
				using var reader = await cmd.ExecuteReaderAsync();
				if (!await reader.ReadAsync())
					throw new InvalidOperationException($"Failed to retrieve folder row after insert: {path}");
				folder = ReadLocalFolder(reader);
			}
		}

		return folder;
	}

	/// <summary>
	/// Removes a folder and all its associated local tracks (including artist links) within a
	/// single transaction to prevent orphaned rows.
	/// </summary>
	public async Task RemoveFolderAsync(int folderId)
	{
		using var connection = _connectionFactory.Create();

		using var transaction = connection.BeginTransaction();

		// Delete artist links first while Tracks rows still exist for the subquery.
		using (var cmd = connection.CreateCommand())
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

		using (var cmd = connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = "DELETE FROM Tracks WHERE FolderId = @folderId AND SourceType = 'local'";
			cmd.Parameters.AddWithValue("@folderId", folderId);
			await cmd.ExecuteNonQueryAsync();
		}

		using (var cmd = connection.CreateCommand())
		{
			cmd.Transaction = transaction;
			cmd.CommandText = "DELETE FROM LocalFolders WHERE Id = @folderId";
			cmd.Parameters.AddWithValue("@folderId", folderId);
			await cmd.ExecuteNonQueryAsync();
		}

		transaction.Commit();
	}

	/// <summary>
	/// Returns the filesystem path of a registered folder, or <see langword="null"/> when
	/// the folder row does not exist.
	/// </summary>
	public async Task<string?> GetFolderPathAsync(int folderId)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "SELECT Path FROM LocalFolders WHERE Id = @id";
		cmd.Parameters.AddWithValue("@id", folderId);
		return (string?)await cmd.ExecuteScalarAsync();
	}

	/// <summary>
	/// Returns local tracks optionally filtered by <paramref name="folderId"/>,
	/// ordered by Artist → Album → Title.
	/// </summary>
	public async Task<IReadOnlyList<Track>> GetTracksAsync(int? folderId = null)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();

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
	/// Returns the number of local tracks, optionally filtered by <paramref name="folderId"/>.
	/// Cheaper than loading full track rows when only a count is needed.
	/// </summary>
	public async Task<int> GetTrackCountAsync(int? folderId = null)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();

		if (folderId.HasValue)
		{
			cmd.CommandText = "SELECT COUNT(*) FROM Tracks WHERE SourceType = 'local' AND FolderId = @folderId";
			cmd.Parameters.AddWithValue("@folderId", folderId.Value);
		}
		else
		{
			cmd.CommandText = "SELECT COUNT(*) FROM Tracks WHERE SourceType = 'local'";
		}

		object? result = await cmd.ExecuteScalarAsync();
		return result is long count ? (int)count : 0;
	}

	/// <summary>
	/// Searches local tracks by title, artist, or album (case-insensitive LIKE substring match).
	/// Returns at most 100 results ordered by Artist, Title.
	/// </summary>
	public async Task<IReadOnlyList<Track>> SearchTracksAsync(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
			return [];

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();

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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();

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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		bool isUnknownArtist = artistName == "Неизвестный исполнитель";
		bool isUnknownAlbum = albumName == "Без альбома";
		string artistFilter = isUnknownArtist ? "(Artist IS NULL OR Artist = '')" : "Artist = @artist";
		string albumFilter = isUnknownAlbum ? "(Album IS NULL OR Album = '')" : "Album = @album";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT {TrackProjection}
			FROM Tracks
			WHERE SourceType = 'local' AND {artistFilter} AND {albumFilter}{folderFilter}
			ORDER BY TrackNumber, Title";

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
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
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

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		bool isUnknown = albumName == "Без альбома";
		string albumFilter = isUnknown ? "(Album IS NULL OR Album = '')" : "Album = @album";
		string folderFilter = folderId.HasValue ? " AND FolderId = @folderId" : "";
		cmd.CommandText = $@"
			SELECT {TrackProjection}
			FROM Tracks
			WHERE SourceType = 'local' AND {albumFilter}{folderFilter}
			ORDER BY TrackNumber, Title";

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
	// Helpers for the scanner
	// -------------------------------------------------------------------------

	/// <summary>
	/// Deletes tracks of the folder that are no longer present on disk, along with their
	/// artist links. The passed connection is owned by the caller (the scan reuses it).
	/// </summary>
	public async Task RemoveMissingLocalTracksAsync(SqliteConnection connection, int folderId, IReadOnlyCollection<string> currentFilePaths)
	{
		var currentFilePathSet = new HashSet<string>(currentFilePaths, StringComparer.OrdinalIgnoreCase);
		var staleTrackIds = new List<string>();

		using (var cmd = connection.CreateCommand())
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

		using var transaction = connection.BeginTransaction();

		using (var deleteTrackArtistsCmd = connection.CreateCommand())
		using (var deleteTracksCmd = connection.CreateCommand())
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

	/// <summary>Updates the folder's last-scan timestamp. The passed connection is owned by the caller.</summary>
	public async Task UpdateFolderLastScannedAtAsync(SqliteConnection connection, int folderId)
	{
		long scannedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "UPDATE LocalFolders SET LastScannedAt = @scannedAt WHERE Id = @id";
		cmd.Parameters.AddWithValue("@scannedAt", scannedAt);
		cmd.Parameters.AddWithValue("@id", folderId);
		await cmd.ExecuteNonQueryAsync();
	}

	private static Track ReadTrack(SqliteDataReader reader)
	{
		string trackId = reader.GetString(0);
		string artist = reader.IsDBNull(1) ? "" : reader.GetString(1);
		string title = reader.IsDBNull(2) ? "" : reader.GetString(2);
		string album = reader.IsDBNull(3) ? "" : reader.GetString(3);
		long? durationMs = reader.IsDBNull(4) ? null : reader.GetInt64(4);
		int? year = reader.IsDBNull(5) ? null : reader.GetInt32(5);
		int? trackNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6);
		string? coverUrl = reader.IsDBNull(7) ? null : reader.GetString(7);
		string? remoteCoverUrl = reader.IsDBNull(8) ? null : reader.GetString(8);
		string? localCoverPath = reader.IsDBNull(9) ? null : reader.GetString(9);
		string? genresJson = reader.IsDBNull(10) ? null : reader.GetString(10);
		string? albumId = reader.IsDBNull(11) ? null : reader.GetString(11);
		string sourceType = reader.IsDBNull(12) ? SourceIds.Local : reader.GetString(12);
		string sourceTrackId = reader.IsDBNull(13) ? trackId : reader.GetString(13);
		string? localFilePath = reader.IsDBNull(14) ? null : reader.GetString(14);

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
			TrackNumber = trackNumber,
			CoverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(sourceType, coverUrl, remoteCoverUrl, localCoverPath),
			RemoteCoverUrl = CoverMetadataResolver.ResolveRemoteCoverUrl(sourceType, coverUrl, remoteCoverUrl),
			LocalCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(sourceType, coverUrl, localCoverPath),
			Genres = genres,
			SourceType = sourceType,
			SourceTrackId = sourceTrackId,
			LocalFilePath = localFilePath,
			AlbumInfo = albumInfo,
		};
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
