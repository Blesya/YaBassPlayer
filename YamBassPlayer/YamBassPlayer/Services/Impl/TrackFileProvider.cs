using Serilog;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Track;

namespace YamBassPlayer.Services.Impl;

public class TrackFileProvider : ITrackFileProvider
{
	private const int CopyBufferSize = 81920;
	private const int MaxPercentDuringCopy = 99;
	private const long IndeterminateReportStepBytes = 262144;

	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly string _tracksFolder;
	private readonly ITrackSourceDetector _sourceDetector;

	public TrackFileProvider(YandexMusicApi api, AuthStorage storage, string tracksFolder, ITrackSourceDetector sourceDetector)
	{
		_api = api;
		_storage = storage;
		_tracksFolder = tracksFolder;
		_sourceDetector = sourceDetector;

		if (!Directory.Exists(_tracksFolder))
		{
			Directory.CreateDirectory(_tracksFolder);
		}
	}

	public string GetTrackPath(string trackId)
	{
		return Path.Combine(_tracksFolder, $"{trackId}.mp3");
	}

	public bool IsTrackDownloaded(string trackId)
	{
		if (_sourceDetector.IsLocal(trackId))
			return File.Exists(trackId);
		return File.Exists(GetTrackPath(trackId));
	}

	public async Task<string> DownloadTrackAsync(string trackId, IProgress<DownloadProgress>? progress = null)
	{
		Logging.LogBeforeCall();

		// Local tracks: trackId is the absolute file path — no download needed
		if (_sourceDetector.IsLocal(trackId))
			return File.Exists(trackId) ? trackId : string.Empty;

		try
		{
			string filePath = GetTrackPath(trackId);

			if (File.Exists(filePath))
			{
				Log.Debug("Трек {TrackId} уже скачан: {FilePath}", trackId, filePath);
				ReportCached(filePath, progress);
				return filePath;
			}

			var trackResponse = await _api.Track.GetAsync(_storage, trackId);
			var track = trackResponse?.Result?.FirstOrDefault();

			if (track == null)
			{
				throw new Exception("Не удалось получить информацию о треке");
			}

			Log.Information("Скачивание трека {TrackId}", trackId);
			await DownloadToFileAsync(track, filePath, progress);
			Log.Information("Трек {TrackId} скачан: {FilePath}", trackId, filePath);

			Logging.LogAfterCall();
			return filePath;
		}
		catch (Exception e)
		{
			Log.Error(e, "Не удалось скачать трек {TrackId}", trackId);
			e.Handle(logException: false);
			return string.Empty;
		}
	}

	public async Task<string> GetTrackFilePathAsync(string trackId)
	{
		if (_sourceDetector.IsLocal(trackId))
			return trackId;

		return GetTrackPath(trackId);
	}

	private static void ReportCached(string filePath, IProgress<DownloadProgress>? progress)
	{
		if (progress == null)
			return;

		long size = new FileInfo(filePath).Length;
		progress.Report(new DownloadProgress(size, size));
	}

	/// <summary>
	/// Streams the track into a temporary ".part" file and only promotes it to the final
	/// path once the whole file is written, so an interrupted download is never mistaken
	/// for a cached one by <see cref="IsTrackDownloaded"/>.
	/// </summary>
	private async Task DownloadToFileAsync(YTrack track, string filePath, IProgress<DownloadProgress>? progress)
	{
		string tempPath = $"{filePath}.part";

		try
		{
			string url = await _api.Track.GetFileLinkAsync(_storage, track);

			using var request = new HttpRequestMessage(HttpMethod.Get, url);
			using var response = await _storage.Provider.GetWebResponseAsync(request, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			// Content-Length is the reliable source of the total size; YTrack.FileSize is often 0.
			long totalBytes = response.Content.Headers.ContentLength ?? track.FileSize;

			await using (var stream = await response.Content.ReadAsStreamAsync())
			await using (var file = File.Create(tempPath))
			{
				await CopyWithProgressAsync(stream, file, totalBytes, progress);
			}

			File.Move(tempPath, filePath, overwrite: true);

			long finalSize = new FileInfo(filePath).Length;
			progress?.Report(new DownloadProgress(finalSize, totalBytes > 0 ? totalBytes : finalSize));
		}
		finally
		{
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
	}

	private static async Task CopyWithProgressAsync(Stream source, Stream destination, long totalBytes, IProgress<DownloadProgress>? progress)
	{
		var buffer = new byte[CopyBufferSize];
		long copied = 0;
		long lastIndeterminateReport = -1;
		int lastPercent = -1;

		int read;
		while ((read = await source.ReadAsync(buffer)) > 0)
		{
			await destination.WriteAsync(buffer.AsMemory(0, read));
			copied += read;

			if (progress == null)
				continue;

			if (totalBytes > 0)
			{
				int percent = (int)Math.Min(copied * 100 / totalBytes, MaxPercentDuringCopy);
				if (percent == lastPercent)
					continue;

				lastPercent = percent;
				progress.Report(new DownloadProgress(copied, totalBytes));
			}
			else if (copied - lastIndeterminateReport >= IndeterminateReportStepBytes)
			{
				lastIndeterminateReport = copied;
				progress.Report(new DownloadProgress(copied, 0));
			}
		}
	}
}
