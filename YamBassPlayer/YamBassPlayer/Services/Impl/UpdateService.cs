using System.Security.Cryptography;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Скачивание дистрибутива во временный каталог с прогрессом и проверкой контрольной суммы.
/// </summary>
public sealed class UpdateService : IUpdateService
{
	private const int BufferSize = 81920;

	private static readonly HttpClient SharedHttpClient = CreateHttpClient();

	private readonly HttpClient _httpClient;
	private readonly IReleaseFeed _releaseFeed;
	private readonly string _tempRoot;

	public UpdateService(IReleaseFeed releaseFeed)
		: this(releaseFeed, SharedHttpClient, Path.Combine(Path.GetTempPath(), "YamBassPlayer"))
	{
	}

	internal UpdateService(IReleaseFeed releaseFeed, HttpClient httpClient, string tempRoot)
	{
		_releaseFeed = releaseFeed;
		_httpClient = httpClient;
		_tempRoot = tempRoot;
	}

	public Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken) => _releaseFeed.GetLatestAsync(cancellationToken);

	public async Task<string> DownloadAsync(UpdateInfo info, IProgress<int>? progress, CancellationToken cancellationToken)
	{
		string directory = Path.Combine(_tempRoot, $"update-{info.Version}");
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, info.AssetName);

		using (HttpResponseMessage response = await _httpClient.GetAsync(
			info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
		{
			response.EnsureSuccessStatusCode();

			long total = response.Content.Headers.ContentLength ?? info.SizeBytes;

			await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
			await using FileStream target = File.Create(path);

			byte[] buffer = new byte[BufferSize];
			long received = 0;
			int reportedPercent = -1;

			int read;
			while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
			{
				await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
				received += read;

				if (total <= 0)
					continue;

				int percent = (int)(received * 100 / total);
				if (percent == reportedPercent)
					continue;

				reportedPercent = percent;
				progress?.Report(percent);
			}
		}

		await VerifyChecksumAsync(path, info.ChecksumUrl, cancellationToken);
		return path;
	}

	private async Task VerifyChecksumAsync(string filePath, string? checksumUrl, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(checksumUrl))
			return;

		string content = await _httpClient.GetStringAsync(checksumUrl, cancellationToken);
		string expected = content
			.TrimStart('\uFEFF')
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault() ?? string.Empty;

		if (expected.Length == 0)
			return;

		string actual;

		await using (FileStream stream = File.OpenRead(filePath))
		{
			actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
		}

		if (actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
			return;

		File.Delete(filePath);
		throw new InvalidDataException("Контрольная сумма архива не совпала — файл повреждён, обновление отменено.");
	}

	private static HttpClient CreateHttpClient()
	{
		HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.AppInfo.RepositoryName);
		return client;
	}
}
