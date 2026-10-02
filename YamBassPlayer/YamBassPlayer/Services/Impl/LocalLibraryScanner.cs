using System.Security.Cryptography;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Рекурсивно сканирует папки локальной библиотеки, читает метаданные аудиофайлов через
/// TagLib# и извлекает обложки. Не управляет блокировкой записи — её удерживает вызывающий
/// код (фасад), чтобы охватить всю операцию сканирования.
/// </summary>
public sealed class LocalLibraryScanner
{
	private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp3", ".flac", ".ogg", ".wav", ".m4a"
	};

	// Reporting progress on every scanned file floods the UI; report every N-th file instead.
	private const int ProgressEveryNFiles = 20;

	private readonly IDbConnectionFactory _connectionFactory;
	private readonly string _coversFolder;
	private readonly LocalLibraryRepository _repository;

	public event Action<string>? OnScanProgress;
	public event Action<int>? OnScanCompleted;

	public LocalLibraryScanner(IDbConnectionFactory connectionFactory, string coversFolder, LocalLibraryRepository repository)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(coversFolder);
		ArgumentNullException.ThrowIfNull(repository);
		_connectionFactory = connectionFactory;
		_coversFolder = coversFolder;
		_repository = repository;

		if (!Directory.Exists(_coversFolder))
			Directory.CreateDirectory(_coversFolder);
	}

	/// <summary>
	/// Recursively scans a registered folder for audio files, reads ID3 tags via TagLib#,
	/// and upserts track records into the database. Progress is reported per file name.
	/// </summary>
	/// <returns>Number of tracks found or updated.</returns>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="folderId"/> is not found.</exception>
	public async Task<int> ScanFolderAsync(int folderId, IProgress<string>? progress = null)
	{
		Logging.LogBeforeCall();

		string? folderPath = await _repository.GetFolderPathAsync(folderId);
		if (folderPath is null)
			throw new InvalidOperationException($"Folder with id {folderId} not found.");

		using var connection = _connectionFactory.Create();

		if (!Directory.Exists(folderPath))
		{
			await _repository.RemoveMissingLocalTracksAsync(connection, folderId, []);
			await _repository.UpdateFolderLastScannedAtAsync(connection, folderId);
			OnScanCompleted?.Invoke(0);
			return 0;
		}

		List<string> audioFiles = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
			.Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
			.ToList();

		long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		int count = 0;

		// One transaction for the whole folder plus reusable prepared commands: creating a
		// transaction and re-parsing SQL per file dominates scan time on large libraries.
		using (var transaction = connection.BeginTransaction())
		using (var writer = new LocalTrackWriter(connection, transaction))
		{
			foreach (string filePath in audioFiles)
			{
				// Progress is throttled: reporting every file floods the UI with repaints.
				if (count % ProgressEveryNFiles == 0)
				{
					string fileName = Path.GetFileName(filePath);
					progress?.Report(fileName);
					OnScanProgress?.Invoke(fileName);
				}

				Track track = ParseTrackFromFile(filePath);
				writer.Save(track, folderId, updatedAt);
				count++;
			}

			transaction.Commit();
		}

		await _repository.RemoveMissingLocalTracksAsync(connection, folderId, audioFiles);
		await _repository.UpdateFolderLastScannedAtAsync(connection, folderId);

		OnScanCompleted?.Invoke(count);
		Logging.LogAfterCall();
		return count;
	}

	/// <summary>
	/// Builds a <see cref="Track"/> from a local audio file using TagLib# ID3 tag reading.
	/// Falls back to filename heuristics when tags are unavailable or the file is corrupt.
	/// </summary>
	public Track ParseTrackFromFile(string filePath)
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
				SourceType = SourceIds.Local,
				SourceTrackId = filePath,
				LocalFilePath = filePath,
				Artists = artists,
				AlbumInfo = albumInfo,
				Year = tag.Year > 0 ? (int?)tag.Year : null,
				TrackNumber = tag.Track > 0 ? (int?)tag.Track : null,
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
			SourceType = SourceIds.Local,
			SourceTrackId = filePath,
			LocalFilePath = filePath,
			CoverUrl = coverUrl,
			LocalCoverPath = coverUrl,
		};
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
}
