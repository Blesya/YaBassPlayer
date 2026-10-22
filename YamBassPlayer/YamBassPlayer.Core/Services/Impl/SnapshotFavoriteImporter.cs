using System.Text.Json;
using Serilog;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Дочитывает избранное из снимка v1, если файл состояния источника ещё не создан.
/// Нужен, потому что источник читает состояние в конструкторе — раньше, чем снимок
/// успевает мигрировать через <see cref="PlaylistStateStore"/>.
/// </summary>
public sealed class SnapshotFavoriteImporter(string playlistStatePath, IPluginStateFile pluginState)
    : ISnapshotFavoriteImporter
{
    private const string YandexPluginId = "yandex-music";
    private const string FavoriteTrackIdsKey = "favoriteTrackIds";
    private const string CustomPlaylistIdsKey = "customPlaylistIds";

    public void ImportIfMissing()
    {
        try
        {
            if (!File.Exists(playlistStatePath))
                return;

            using var document = JsonDocument.Parse(File.ReadAllText(playlistStatePath));
            if (!PlaylistSnapshotMigration.TryMigrate(document, out MigratedSnapshot? snapshot) || snapshot is null)
                return;

            if (pluginState.Get<List<string>>(YandexPluginId, FavoriteTrackIdsKey) is null
                && snapshot.FavoriteTrackIds.Count > 0)
            {
                pluginState.Set(YandexPluginId, FavoriteTrackIdsKey, snapshot.FavoriteTrackIds.ToList());
            }

            if (pluginState.Get<Dictionary<string, List<string>>>(YandexPluginId, CustomPlaylistIdsKey) is null
                && snapshot.CustomPlaylistTrackIds.Count > 0)
            {
                pluginState.Set(
                    YandexPluginId,
                    CustomPlaylistIdsKey,
                    snapshot.CustomPlaylistTrackIds.ToDictionary(pair => pair.Key, pair => pair.Value));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось перенести избранное из снимка плейлистов");
        }
    }
}
