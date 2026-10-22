using System.Net.Http;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Обложки треков: локальные берутся из ID3/папки, удалённые — URL у источника
/// и файл в кэше обложек.
/// </summary>
public sealed class CoverProvider(
    string coversFolder,
    IDbConnectionFactory connectionFactory,
    IMusicSourceRegistry musicSourceRegistry) : ICoverProvider
{
    private static readonly HttpClient HttpClient = new();

    private sealed record TrackCoverMetadata(
        string? CoverUrl,
        string? RemoteCoverUrl,
        string? LocalCoverPath);

    public string GetCoverPath(TrackKey key)
    {
        string fileName = $"{SafeFileName(key)}.jpg";
        return key.SourceId == LocalMusicSource.Id
            ? Path.Combine(coversFolder, fileName)
            : Path.Combine(coversFolder, key.SourceId.Value, fileName);
    }

    /// <summary>Плоский путь до раскладки по источникам: только для чтения старого кэша.</summary>
    private string GetFlatCoverPath(TrackKey key)
        => Path.Combine(coversFolder, $"{SafeFileName(key)}.jpg");

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

    public bool IsCoverDownloaded(TrackKey key)
        => File.Exists(GetCoverPath(key)) || File.Exists(GetFlatCoverPath(key));

    public async Task<string> DownloadCoverAsync(Track track)
    {
        TrackKey key = track.Key;

        // Local tracks use an absolute file path as their ID — route them to the local handler.
        if (key.SourceId == LocalMusicSource.Id || !string.IsNullOrEmpty(track.LocalFilePath))
            return await GetLocalCoverAsync(track.LocalFilePath ?? key.TrackId).ConfigureAwait(false);

        try
        {
            string filePath = GetCoverPath(key);
            if (File.Exists(filePath))
                return filePath;

            string flatPath = GetFlatCoverPath(key);
            if (File.Exists(flatPath))
                return flatPath;

            string? coverUrl = ResolveRemoteCoverUrl(await GetTrackCoverMetadataAsync(key).ConfigureAwait(false));

            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                IMusicSource? source = musicSourceRegistry.Get(key.SourceId);
                coverUrl = source is null ? null : await source.GetCoverUrlAsync(key.TrackId).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(coverUrl))
                return string.Empty;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

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
    /// Resolves cover art for a local audio file: stored path, embedded ID3 art,
    /// then well-known image files next to the audio.
    /// </summary>
    private async Task<string> GetLocalCoverAsync(string filePath)
    {
        string? localCoverPath = await GetStoredLocalCoverPathAsync(new TrackKey(LocalMusicSource.Id, filePath)).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(localCoverPath) && File.Exists(localCoverPath))
            return localCoverPath;

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var picture = tagFile.Tag.Pictures?.FirstOrDefault();
            if (picture?.Data?.Data != null)
            {
                string coverFileName = Convert.ToHexString(
                    System.Security.Cryptography.MD5.HashData(
                        System.Text.Encoding.UTF8.GetBytes(filePath))) + ".jpg";
                string coverPath = Path.Combine(coversFolder, coverFileName);
                if (!File.Exists(coverPath))
                    File.WriteAllBytes(coverPath, picture.Data.Data);
                return coverPath;
            }
        }
        catch { /* corrupt or unsupported file — fall through to folder art */ }

        string dir = Path.GetDirectoryName(filePath) ?? "";
        foreach (string name in new[] { "cover.jpg", "folder.jpg", "album.jpg", "cover.png", "folder.png" })
        {
            string candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private async Task<string?> GetStoredLocalCoverPathAsync(TrackKey key)
    {
        TrackCoverMetadata? coverMetadata = await GetTrackCoverMetadataAsync(key).ConfigureAwait(false);
        if (coverMetadata == null)
            return null;

        if (!string.IsNullOrWhiteSpace(coverMetadata.LocalCoverPath))
            return coverMetadata.LocalCoverPath;

        return !string.IsNullOrWhiteSpace(coverMetadata.CoverUrl) && Path.IsPathRooted(coverMetadata.CoverUrl)
            ? coverMetadata.CoverUrl
            : null;
    }

    private async Task<TrackCoverMetadata?> GetTrackCoverMetadataAsync(TrackKey key)
    {
        try
        {
            using var connection = connectionFactory.Create();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT CoverUrl, RemoteCoverUrl, LocalCoverPath
                FROM Tracks
                WHERE TrackId = @id AND SourceType = @source
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@id", key.TrackId);
            cmd.Parameters.AddWithValue("@source", key.SourceId.Value);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;

            return new TrackCoverMetadata(
                CoverUrl: reader.IsDBNull(0) ? null : reader.GetString(0),
                RemoteCoverUrl: reader.IsDBNull(1) ? null : reader.GetString(1),
                LocalCoverPath: reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Не удалось прочитать метаданные обложки для {TrackId}", key.TrackId);
            return null;
        }
    }

    private static string? ResolveRemoteCoverUrl(TrackCoverMetadata? coverMetadata)
    {
        if (coverMetadata == null)
            return null;

        string? coverUrl = !string.IsNullOrWhiteSpace(coverMetadata.RemoteCoverUrl)
            ? coverMetadata.RemoteCoverUrl
            : !string.IsNullOrWhiteSpace(coverMetadata.CoverUrl) && !Path.IsPathRooted(coverMetadata.CoverUrl)
                ? coverMetadata.CoverUrl
                : null;

        return string.IsNullOrWhiteSpace(coverUrl)
            ? null
            : CoverUrl.Normalize(coverUrl);
    }
}
