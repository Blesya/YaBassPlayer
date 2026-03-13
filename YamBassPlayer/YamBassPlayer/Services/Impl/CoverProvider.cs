using System.Net.Http;
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

	public CoverProvider(YandexMusicApi api, AuthStorage storage, string coversFolder)
	{
		_api = api;
		_storage = storage;
		_coversFolder = coversFolder;

		if (!Directory.Exists(_coversFolder))
		{
			Directory.CreateDirectory(_coversFolder);
		}
	}

	public string GetCoverPath(string trackId)
	{
		return Path.Combine(_coversFolder, $"{trackId}.jpg");
	}

	public bool IsCoverDownloaded(string trackId)
	{
		return File.Exists(GetCoverPath(trackId));
	}

	public async Task<string> DownloadCoverAsync(string trackId)
	{
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
