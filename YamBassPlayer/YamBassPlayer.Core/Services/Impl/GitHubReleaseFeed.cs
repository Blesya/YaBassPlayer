using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamBassPlayer.Constants;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Читает последний релиз из GitHub Releases API. Берём только релизы с тегом
/// строго вида <c>vX.Y.Z</c> и ассетом <c>YamBassPlayer-X.Y.Z-win-x64.zip</c> —
/// всё остальное считается «обновления нет».
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
	private const string AssetSuffix = "-win-x64.zip";
	private const string ChecksumSuffix = ".sha256";

	private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
	private static readonly HttpClient SharedHttpClient = CreateHttpClient();

	private readonly HttpClient _httpClient;
	private readonly string _feedUrl;

	public GitHubReleaseFeed()
		: this(SharedHttpClient, $"https://api.github.com/repos/{AppInfo.RepositoryOwner}/{AppInfo.RepositoryName}/releases/latest")
	{
	}

	internal GitHubReleaseFeed(HttpClient httpClient, string feedUrl)
	{
		_httpClient = httpClient;
		_feedUrl = feedUrl;
	}

	public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await _httpClient.GetAsync(_feedUrl, cancellationToken);

		// Без токена GitHub даёт 60 запросов в час на IP — это не «ошибка сети», и текст должен быть внятным.
		if (response.StatusCode == HttpStatusCode.Forbidden)
			throw new HttpRequestException("GitHub ограничил частоту запросов (не больше 60 в час на IP). Попробуйте позже.");

		response.EnsureSuccessStatusCode();

		await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		return Parse(document.RootElement);
	}

	/// <summary>Разбирает ответ API. Открыт ради тестов: сети для проверки не требуется.</summary>
	public static UpdateInfo? Parse(JsonElement release)
	{
		if (!release.TryGetProperty("tag_name", out JsonElement tagElement) || tagElement.ValueKind != JsonValueKind.String)
			return null;

		string tag = tagElement.GetString() ?? string.Empty;
		string versionText = tag.StartsWith('v') ? tag[1..] : tag;

		if (!VersionPattern.IsMatch(versionText) || !System.Version.TryParse(versionText, out System.Version? parsedVersion))
			return null;

		System.Version version = parsedVersion!;

		if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
			return null;

		string assetName = $"{AppInfo.AppName}-{versionText}{AssetSuffix}";
		string? downloadUrl = null;
		string? checksumUrl = null;
		long size = 0;

		foreach (JsonElement asset in assets.EnumerateArray())
		{
			string? name = asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
			if (name is null)
				continue;

			if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
			{
				downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
					? urlElement.GetString()
					: null;

				size = asset.TryGetProperty("size", out JsonElement sizeElement) && sizeElement.TryGetInt64(out long parsedSize)
					? parsedSize
					: 0;
			}
			else if (string.Equals(name, assetName + ChecksumSuffix, StringComparison.OrdinalIgnoreCase))
			{
				checksumUrl = asset.TryGetProperty("browser_download_url", out JsonElement checksumElement)
					? checksumElement.GetString()
					: null;
			}
		}

		if (string.IsNullOrWhiteSpace(downloadUrl))
			return null;

		string? notes = release.TryGetProperty("body", out JsonElement bodyElement) && bodyElement.ValueKind == JsonValueKind.String
			? bodyElement.GetString()
			: null;

		return new UpdateInfo(version, tag, assetName, downloadUrl!, size, notes, checksumUrl);
	}

	private static HttpClient CreateHttpClient()
	{
		HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.RepositoryName);
		client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return client;
	}
}
