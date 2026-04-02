using System.Net.Http;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Extensions;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public sealed class CoverProvider : ICoverProvider
{
	private static readonly HttpClient HttpClient = new();
	private sealed record TrackCoverMetadata(
		string SourceType,
		string? CoverUrl,
		string? RemoteCoverUrl,
		string? LocalCoverPath);

	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly string _coversFolder;
	private readonly SqliteConnection _connection;

	public CoverProvider(YandexMusicApi api, AuthStorage storage, string coversFolder, SqliteConnection connection)
	{
		_api = api;
		_storage = storage;
		_coversFolder = coversFolder;
		_connection = connection;

		if (!Directory.Exists(_coversFolder))
		{
			Directory.CreateDirectory(_coversFolder);
		}
	}

	public string GetCoverPath(string trackId)
	{
		// Local tracks use the full file path as their ID; hashing it prevents invalid path characters.
		string safeId = Path.IsPathRooted(trackId)
			? Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(trackId)))
			: trackId;
		return Path.Combine(_coversFolder, $"{safeId}.jpg");
	}

	public bool IsCoverDownloaded(string trackId)
	{
		return File.Exists(GetCoverPath(trackId));
	}

	public async Task<string> DownloadCoverAsync(string trackId)
	{
		// Local tracks use an absolute file path as their ID — route them to the local handler.
		if (Path.IsPathRooted(trackId))
			return await GetLocalCoverAsync(trackId).ConfigureAwait(false);

		try
		{
			string filePath = GetCoverPath(trackId);
			if (File.Exists(filePath))
			{
				return filePath;
			}

			TrackCoverMetadata? coverMetadata = await GetTrackCoverMetadataAsync(trackId).ConfigureAwait(false);
			string? coverUrl = ResolveRemoteCoverUrl(coverMetadata);
			if (string.IsNullOrWhiteSpace(coverUrl))
			{
				var trackResponse = await _api.Track.GetAsync(_storage, trackId);
				YTrack? track = trackResponse?.Result?.FirstOrDefault();
				if (track == null)
				{
					return string.Empty;
				}

				coverUrl = track.ToTrack().RemoteCoverUrl;
			}

			if (string.IsNullOrWhiteSpace(coverUrl))
			{
				return string.Empty;
			}

			byte[] bytes = await HttpClient.GetByteArrayAsync(coverUrl);
			await File.WriteAllBytesAsync(filePath, bytes);
			return filePath;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return string.Empty;
		}
	}

	/// <summary>
	/// Resolves cover art for a local audio file. Checks the DB for a previously extracted
	/// cover, then tries embedded ID3 art, and finally looks for well-known image files in the
	/// same directory (cover.jpg / folder.jpg / album.jpg).
	/// </summary>
	private async Task<string> GetLocalCoverAsync(string filePath)
	{
		// 1. Return the explicit local cover path already stored in the DB.
		string? localCoverPath = await GetStoredLocalCoverPathAsync(filePath).ConfigureAwait(false);
		if (!string.IsNullOrEmpty(localCoverPath) && File.Exists(localCoverPath))
			return localCoverPath;

		// 2. Try to extract an embedded cover from ID3 tags.
		try
		{
			using var tagFile = TagLib.File.Create(filePath);
			var picture = tagFile.Tag.Pictures?.FirstOrDefault();
			if (picture?.Data?.Data != null)
			{
				string coverFileName = Convert.ToHexString(
					System.Security.Cryptography.MD5.HashData(
						System.Text.Encoding.UTF8.GetBytes(filePath))) + ".jpg";
				string coverPath = Path.Combine(_coversFolder, coverFileName);
				if (!File.Exists(coverPath))
					File.WriteAllBytes(coverPath, picture.Data.Data);
				return coverPath;
			}
		}
		catch { /* corrupt or unsupported file — fall through to folder art */ }

		// 3. Fallback: look for cover/folder.jpg in the same directory.
		string dir = Path.GetDirectoryName(filePath) ?? "";
		foreach (string name in new[] { "cover.jpg", "folder.jpg", "album.jpg", "cover.png", "folder.png" })
		{
			string candidate = Path.Combine(dir, name);
			if (File.Exists(candidate))
				return candidate;
		}

		return string.Empty;
	}

	private async Task<string?> GetStoredLocalCoverPathAsync(string trackId)
	{
		TrackCoverMetadata? coverMetadata = await GetTrackCoverMetadataAsync(trackId).ConfigureAwait(false);
		if (coverMetadata == null)
			return null;

		if (!string.IsNullOrWhiteSpace(coverMetadata.LocalCoverPath))
			return coverMetadata.LocalCoverPath;

		return IsLocalSourceType(coverMetadata.SourceType)
			? coverMetadata.CoverUrl
			: null;
	}

	private async Task<TrackCoverMetadata?> GetTrackCoverMetadataAsync(string trackId)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COALESCE(SourceType, 'yandex'), CoverUrl, RemoteCoverUrl, LocalCoverPath
			FROM Tracks
			WHERE TrackId = @id
			LIMIT 1
			""";
		cmd.Parameters.AddWithValue("@id", trackId);

		using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
		if (!await reader.ReadAsync().ConfigureAwait(false))
			return null;

		return new TrackCoverMetadata(
			SourceType: reader.GetString(0),
			CoverUrl: reader.IsDBNull(1) ? null : reader.GetString(1),
			RemoteCoverUrl: reader.IsDBNull(2) ? null : reader.GetString(2),
			LocalCoverPath: reader.IsDBNull(3) ? null : reader.GetString(3));
	}

	private static string? ResolveRemoteCoverUrl(TrackCoverMetadata? coverMetadata)
	{
		if (coverMetadata == null)
			return null;

		string? coverUrl = !string.IsNullOrWhiteSpace(coverMetadata.RemoteCoverUrl)
			? coverMetadata.RemoteCoverUrl
			: IsLocalSourceType(coverMetadata.SourceType)
				? null
				: coverMetadata.CoverUrl;

		return string.IsNullOrWhiteSpace(coverUrl)
			? null
			: NormalizeCoverUrl(coverUrl);
	}

	private static bool IsLocalSourceType(string sourceType)
		=> string.Equals(sourceType, "local", StringComparison.OrdinalIgnoreCase);

	private static string NormalizeCoverUrl(string rawUrl)
	{
		string normalized = rawUrl.Replace("%%", "400x400");
		if (normalized.StartsWith("//"))
		{
			return $"https:{normalized}";
		}

		if (!normalized.StartsWith("http://") && !normalized.StartsWith("https://"))
		{
			return $"https://{normalized.TrimStart('/')}";
		}

		return normalized;
	}
}
