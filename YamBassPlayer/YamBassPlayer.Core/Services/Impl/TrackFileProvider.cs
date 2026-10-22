using System.Collections.Concurrent;
using Serilog;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Файловый кэш треков: имя файла по источнику, дедупликация одновременных загрузок
/// и учёт состояния. Сам файл добывает источник через <see cref="IMusicSource"/>.
/// </summary>
public sealed class TrackFileProvider(
    string tracksFolder,
    ITrackDownloadTracker downloadTracker,
    IMusicSourceRegistry musicSourceRegistry) : ITrackFileProvider
{
    /// <summary>
    /// Активные загрузки по ключу трека. Повторный запрос того же трека
    /// присоединяется к уже идущей загрузке, а не запускает вторую.
    /// </summary>
    private readonly ConcurrentDictionary<TrackKey, Lazy<Task<string>>> _inFlight = new();

    public string GetTrackPath(TrackKey key)
        => Path.Combine(tracksFolder, key.SourceId.Value, $"{SafeFileName(key)}.mp3");

    /// <summary>Плоский путь до раскладки по источникам: только для чтения старого кэша.</summary>
    private string GetFlatTrackPath(TrackKey key)
        => Path.Combine(tracksFolder, $"{SafeFileName(key)}.mp3");

    /// <summary>
    /// Идентификатор трека как имя файла, если он безопасен; иначе хеш — локальные
    /// идентификаторы являются путями и содержат недопустимые символы.
    /// </summary>
    private static string SafeFileName(TrackKey key)
    {
        string id = key.TrackId;
        if (!Path.IsPathRooted(id)
            && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !id.Contains(Path.DirectorySeparatorChar)
            && !id.Contains(Path.AltDirectorySeparatorChar))
        {
            return id;
        }

        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id)));
    }

    public bool IsTrackDownloaded(Track track)
    {
        if (IsLocalTrack(track))
            return File.Exists(LocalPath(track));

        return File.Exists(GetTrackPath(track.Key)) || File.Exists(GetFlatTrackPath(track.Key));
    }

    public Task<string> DownloadTrackAsync(Track track, IProgress<DownloadProgress>? progress = null)
    {
        Logging.LogBeforeCall();

        // Local tracks: no download needed.
        if (IsLocalTrack(track))
        {
            string localPath = LocalPath(track);
            return Task.FromResult(File.Exists(localPath) ? localPath : string.Empty);
        }

        TrackKey key = track.Key;

        Lazy<Task<string>> lazy = _inFlight.GetOrAdd(
            key,
            _ => new Lazy<Task<string>>(() => DownloadCoreAsync(track, progress)));

        Task<string> task;
        try
        {
            task = lazy.Value;
        }
        catch
        {
            _inFlight.TryRemove(key, out _);
            throw;
        }

        _ = task.ContinueWith(
            _ => _inFlight.TryRemove(new KeyValuePair<TrackKey, Lazy<Task<string>>>(key, lazy)),
            TaskScheduler.Default);

        return task;
    }

    private async Task<string> DownloadCoreAsync(Track track, IProgress<DownloadProgress>? progress)
    {
        TrackKey key = track.Key;
        string filePath = GetTrackPath(key);
        string flatPath = GetFlatTrackPath(key);

        if (File.Exists(flatPath))
        {
            Log.Debug("Трек {TrackId} найден в старом кэше: {FilePath}", key.TrackId, flatPath);
            downloadTracker.MarkCached(key);
            return flatPath;
        }

        if (File.Exists(filePath))
        {
            Log.Debug("Трек {TrackId} уже скачан: {FilePath}", key.TrackId, filePath);
            downloadTracker.MarkCached(key);
            return filePath;
        }

        downloadTracker.MarkDownloading(key);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var progressAdapter = new ActionProgress<DownloadProgress>(value =>
        {
            downloadTracker.ReportProgress(key, value);
            progress?.Report(value);
        });

        try
        {
            IMusicSource source = musicSourceRegistry.GetRequired(key.SourceId);
            string result = await source.GetAudioFilePathAsync(key.TrackId, filePath, progressAdapter);

            if (string.IsNullOrWhiteSpace(result))
            {
                downloadTracker.MarkFailed(key);
                return string.Empty;
            }

            downloadTracker.MarkCached(key);
            return result;
        }
        catch (TrackUnavailableException)
        {
            Log.Information("Трек {TrackId} недоступен", key.TrackId);
            downloadTracker.MarkFailed(key);
            throw;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Не удалось скачать трек {TrackId}", key.TrackId);
            downloadTracker.MarkFailed(key);
            new InvalidOperationException(exception.Message).Handle(logException: false);
            return string.Empty;
        }
        finally
        {
            Logging.LogAfterCall();
        }
    }

    private static bool IsLocalTrack(Track track)
        => track.SourceId == LocalMusicSource.Id || !string.IsNullOrEmpty(track.LocalFilePath);

    private static string LocalPath(Track track)
        => track.LocalFilePath ?? track.Id;

    /// <summary>Прогресс сообщается синхронно, чтобы обновления не обгоняли завершение загрузки.</summary>
    private sealed class ActionProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }
}
