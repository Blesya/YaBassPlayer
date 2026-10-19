using System.Collections.Concurrent;
using Serilog;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Common;
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
	private readonly ITrackDownloadTracker _downloadTracker;

	/// <summary>
	/// Активные загрузки по идентификатору трека. Повторный запрос того же трека
	/// присоединяется к уже идущей загрузке, а не запускает вторую (иначе две задачи
	/// пишут в один и тот же файл и получают ошибку доступа).
	/// </summary>
	private readonly ConcurrentDictionary<string, Lazy<DownloadJob>> _inFlight = new();

	public TrackFileProvider(YandexMusicApi api, AuthStorage storage, string tracksFolder, ITrackSourceDetector sourceDetector, ITrackDownloadTracker downloadTracker)
	{
		_api = api;
		_storage = storage;
		_tracksFolder = tracksFolder;
		_sourceDetector = sourceDetector;
		_downloadTracker = downloadTracker;

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

	public Task<string> DownloadTrackAsync(string trackId, IProgress<DownloadProgress>? progress = null)
	{
		Logging.LogBeforeCall();

		// Local tracks: trackId is the absolute file path — no download needed
		if (_sourceDetector.IsLocal(trackId))
			return Task.FromResult(File.Exists(trackId) ? trackId : string.Empty);

		// Lazy гарантирует, что при гонке GetOrAdd фабрика выполнится ровно один раз,
		// поэтому дублирующая загрузка не стартует.
		Lazy<DownloadJob> lazy = _inFlight.GetOrAdd(
			trackId,
			_ => new Lazy<DownloadJob>(() => new DownloadJob(report => DownloadCoreAsync(trackId, report), progress)));

		DownloadJob job;
		try
		{
			job = lazy.Value;
		}
		catch
		{
			_inFlight.TryRemove(trackId, out _);
			throw;
		}

		job.AddObserver(progress);

		Task<string> task = job.Task;
		// Убираем только свою запись: если к моменту завершения появилась новая загрузка,
		// её не трогаем.
		_ = task.ContinueWith(
			_ => _inFlight.TryRemove(new KeyValuePair<string, Lazy<DownloadJob>>(trackId, lazy)),
			TaskScheduler.Default);

		return task;
	}

	public async Task<string> GetTrackFilePathAsync(string trackId)
	{
		if (_sourceDetector.IsLocal(trackId))
			return trackId;

		return GetTrackPath(trackId);
	}

	private async Task<string> DownloadCoreAsync(string trackId, Action<DownloadProgress> report)
	{
		// Прогресс уходит и подписчику (панель статуса), и трекеру (список треков).
		void Report(DownloadProgress progress)
		{
			_downloadTracker.ReportProgress(trackId, progress);
			report(progress);
		}

		try
		{
			string filePath = GetTrackPath(trackId);

			if (File.Exists(filePath))
			{
				Log.Debug("Трек {TrackId} уже скачан: {FilePath}", trackId, filePath);
				ReportCached(filePath, Report);
				_downloadTracker.MarkCached(trackId);
				return filePath;
			}

			_downloadTracker.MarkDownloading(trackId);

			var trackResponse = await _api.Track.GetAsync(_storage, trackId);
			var track = trackResponse?.Result?.FirstOrDefault();

			if (track == null)
			{
				throw new Exception("Не удалось получить информацию о треке");
			}

			Log.Information("Скачивание трека {TrackId}", trackId);

			// У недоступного в регионе трека Яндекс может прислать замену — официальные
			// клиенты играют именно её. Ключ для download-info берём от замены.
			YTrack downloadTrack = track.Available || track.Substituted is null
				? track
				: track.Substituted;

			Log.Information(
				"Скачивание трека {TrackId}: ключ {TrackKey}, доступен {Available}, только по подписке {PremiumOnly}",
				trackId, downloadTrack.GetKey(), track.Available, track.AvailableForPremiumUsers);

			if (!ReferenceEquals(downloadTrack, track))
				Log.Information("Трек {TrackId} недоступен, используется замена {SubstituteId}", track.Id, downloadTrack.Id);

			await DownloadToFileAsync(downloadTrack, filePath, Report);
			Log.Information("Трек {TrackId} скачан: {FilePath}", trackId, filePath);
			_downloadTracker.MarkCached(trackId);

			return filePath;
		}
		catch (Exception e)
		{
			// YErrorResponse не кладёт текст ошибки в Message, поэтому достаём его
			// из тела ответа — иначе в логе остаётся бесполезное «Exception of type ...».
			string reason = DescribeDownloadError(e);
			Log.Error(e, "Не удалось скачать трек {TrackId}: {Reason}", trackId, reason);
			_downloadTracker.MarkFailed(trackId);
			new InvalidOperationException(reason).Handle(logException: false);
			return string.Empty;
		}
	}

	private static string DescribeDownloadError(Exception exception)
	{
		if (exception is YErrorResponse { Error: { } error })
		{
			string? message = string.IsNullOrWhiteSpace(error.Message) ? error.Name : error.Message;
			if (!string.IsNullOrWhiteSpace(message))
				return $"Яндекс.Музыка: {message}";
		}

		return exception.Message;
	}

	private static void ReportCached(string filePath, Action<DownloadProgress> report)
	{
		long size = new FileInfo(filePath).Length;
		report(new DownloadProgress(size, size));
	}

	/// <summary>
	/// Streams the track into a temporary ".part" file and only promotes it to the final
	/// path once the whole file is written, so an interrupted download is never mistaken
	/// for a cached one by <see cref="IsTrackDownloaded"/>. The temp name is unique per
	/// download, so a stray concurrent writer can never collide with it.
	/// </summary>
	private async Task DownloadToFileAsync(YTrack track, string filePath, Action<DownloadProgress> report)
	{
		string tempPath = $"{filePath}.{Guid.NewGuid():N}.part";

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
				await CopyWithProgressAsync(stream, file, totalBytes, report);
			}

			// Defensive: another process may have finished the same track while we were
			// downloading. Never overwrite an existing (possibly playing) file.
			if (File.Exists(filePath))
			{
				ReportCached(filePath, report);
				return;
			}

			File.Move(tempPath, filePath, overwrite: true);

			long finalSize = new FileInfo(filePath).Length;
			report(new DownloadProgress(finalSize, totalBytes > 0 ? totalBytes : finalSize));
		}
		finally
		{
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
	}

	private static async Task CopyWithProgressAsync(Stream source, Stream destination, long totalBytes, Action<DownloadProgress> report)
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

			if (totalBytes > 0)
			{
				int percent = (int)Math.Min(copied * 100 / totalBytes, MaxPercentDuringCopy);
				if (percent == lastPercent)
					continue;

				lastPercent = percent;
				report(new DownloadProgress(copied, totalBytes));
			}
			else if (copied - lastIndeterminateReport >= IndeterminateReportStepBytes)
			{
				lastIndeterminateReport = copied;
				report(new DownloadProgress(copied, 0));
			}
		}
	}

	/// <summary>
	/// Одна загрузка трека, к которой могут присоединиться несколько вызывающих.
	/// Прогресс рассылается всем наблюдателям.
	/// </summary>
	private sealed class DownloadJob
	{
		private readonly object _lock = new();
		private readonly List<IProgress<DownloadProgress>?> _observers;

		public Task<string> Task { get; }

		public DownloadJob(Func<Action<DownloadProgress>, Task<string>> core, IProgress<DownloadProgress>? first)
		{
			_observers = [first];
			Task = core(Report);
		}

		public void AddObserver(IProgress<DownloadProgress>? observer)
		{
			lock (_lock)
				_observers.Add(observer);
		}

		private void Report(DownloadProgress progress)
		{
			IProgress<DownloadProgress>?[] observers;
			lock (_lock)
				observers = _observers.ToArray();

			foreach (var observer in observers)
				observer?.Report(progress);
		}
	}
}
