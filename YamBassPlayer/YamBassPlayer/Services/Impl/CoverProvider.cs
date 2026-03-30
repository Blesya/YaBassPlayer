using System.Net.Http;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Extensions;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Services.Impl;

public sealed class CoverProvider : ICoverProvider
{
	private static readonly HttpClient HttpClient = new();
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

			var trackResponse = await _api.Track.GetAsync(_storage, trackId);
			var track = trackResponse?.Result?.FirstOrDefault();
			if (track == null)
			{
				return string.Empty;
			}

			string? coverUrl = ResolveCoverUrl(track);
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
		// 1. Return the cover URL already stored in the DB (extracted during scan).
		string? dbCoverUrl = await GetCoverUrlFromDbAsync(filePath).ConfigureAwait(false);
		if (!string.IsNullOrEmpty(dbCoverUrl) && File.Exists(dbCoverUrl))
			return dbCoverUrl;

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

	private async Task<string?> GetCoverUrlFromDbAsync(string trackId)
	{
		using var cmd = _connection.CreateCommand();
		cmd.CommandText = "SELECT CoverUrl FROM Tracks WHERE TrackId = @id";
		cmd.Parameters.AddWithValue("@id", trackId);
		var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
		return result as string;
	}

	private static string? ResolveCoverUrl(object track)
	{
		string? direct = TryGetStringProperty(track, "CoverUri", "CoverUrl", "OgImage", "ImageUri", "ImageUrl");
		if (!string.IsNullOrWhiteSpace(direct))
		{
			return NormalizeCoverUrl(direct);
		}

		object? albums = TryGetProperty(track, "Albums");
		if (albums is System.Collections.IEnumerable enumerable)
		{
			foreach (object? album in enumerable)
			{
				if (album == null)
				{
					continue;
				}

				string? albumUri = TryGetStringProperty(album, "CoverUri", "CoverUrl", "OgImage", "ImageUri", "ImageUrl");
				if (!string.IsNullOrWhiteSpace(albumUri))
				{
					return NormalizeCoverUrl(albumUri);
				}
			}
		}

		return null;
	}

	private static object? TryGetProperty(object obj, params string[] propertyNames)
	{
		var type = obj.GetType();
		foreach (var propertyName in propertyNames)
		{
			var property = type.GetProperty(propertyName);
			if (property != null)
			{
				return property.GetValue(obj);
			}
		}

		return null;
	}

	private static string? TryGetStringProperty(object obj, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			var value = TryGetProperty(obj, propertyName);
			if (value is string str && !string.IsNullOrWhiteSpace(str))
			{
				return str;
			}
		}

		return null;
	}

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
