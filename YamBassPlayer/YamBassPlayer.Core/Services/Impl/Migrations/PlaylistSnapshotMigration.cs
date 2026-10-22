using System.Text.Json;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>Имена прежнего перечисления видов плейлиста.</summary>
public enum LegacyPlaylistType
{
    Favorite,
    PlaylistOfTheDaily,
    Custom,
    Cached,
    Top10,
    TopEvenings,
    LocalFavorite,
    LocalSearch,
    TopByDay,
    YandexSearch,
    Artist,
    Queue,
    MyWave,
    LocalFolder,
    LocalArtist,
    LocalAlbum,
    YandexOwnPlaylist,
    YandexLikedPlaylist
}

internal sealed record MigratedSnapshot(
    PlaylistState State,
    IReadOnlyList<string> FavoriteTrackIds,
    IReadOnlyDictionary<string, List<string>> CustomPlaylistTrackIds);

/// <summary>
/// Переносит снимок плейлистов версии 1 в модель источников.
/// Единственное место, где живёт таблица соответствия старых видов плейлистов.
/// </summary>
internal static class PlaylistSnapshotMigration
{
    public static bool TryMigrate(JsonDocument document, out MigratedSnapshot? snapshot)
    {
        snapshot = null;

        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("Version", out JsonElement version)
            || version.ValueKind != JsonValueKind.Number
            || version.GetInt32() != 1)
        {
            return false;
        }

        var playlists = new List<Playlist>();
        if (root.TryGetProperty("Playlists", out JsonElement playlistsElement)
            && playlistsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in playlistsElement.EnumerateArray())
            {
                Playlist? playlist = ToPlaylist(item);
                if (playlist is not null && playlist.Category == PlaylistCategory.Data)
                    playlists.Add(playlist);
            }
        }

        Playlist? lastPlaylist = null;
        if (root.TryGetProperty("LastPlaylist", out JsonElement lastElement)
            && lastElement.ValueKind == JsonValueKind.Object)
        {
            Playlist? migrated = ToPlaylist(lastElement);
            if (migrated is { Category: not PlaylistCategory.Transient })
                lastPlaylist = migrated;
        }

        TrackKey? lastTrack = null;
        if (root.TryGetProperty("LastTrackId", out JsonElement trackElement)
            && trackElement.ValueKind == JsonValueKind.String
            && trackElement.GetString() is { Length: > 0 } lastTrackId)
        {
            SourceId sourceId = Path.IsPathRooted(lastTrackId) ? LocalMusicSource.Id : new SourceId("yandex");
            lastTrack = new TrackKey(sourceId, lastTrackId);
        }

        var favoriteTrackIds = new List<string>();
        if (root.TryGetProperty("FavoriteTrackIds", out JsonElement favoritesElement)
            && favoritesElement.ValueKind == JsonValueKind.Array)
        {
            favoriteTrackIds.AddRange(favoritesElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(id => !string.IsNullOrEmpty(id)));
        }

        var customPlaylistTrackIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (root.TryGetProperty("CustomPlaylistTrackIds", out JsonElement customElement)
            && customElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in customElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                    continue;

                customPlaylistTrackIds[property.Name] = property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();
            }
        }

        DateTime savedAt = DateTime.UtcNow;
        if (root.TryGetProperty("SavedAt", out JsonElement savedElement)
            && savedElement.ValueKind == JsonValueKind.String
            && savedElement.TryGetDateTime(out DateTime parsed))
        {
            savedAt = parsed;
        }

        snapshot = new MigratedSnapshot(
            new PlaylistState
            {
                Playlists = playlists,
                LastPlaylist = lastPlaylist,
                LastTrack = lastTrack,
                SavedAt = savedAt
            },
            favoriteTrackIds,
            customPlaylistTrackIds);

        return true;
    }

    public static (SourceId? SourceId, string Kind, PlaylistCategory Category) MapLegacyType(LegacyPlaylistType type)
        => type switch
        {
            LegacyPlaylistType.Favorite => (new SourceId("yandex"), "favorite", PlaylistCategory.Data),
            LegacyPlaylistType.PlaylistOfTheDaily => (new SourceId("yandex"), "playlist-of-the-day", PlaylistCategory.Data),
            LegacyPlaylistType.Custom => (new SourceId("yandex"), "personal", PlaylistCategory.Data),
            LegacyPlaylistType.YandexOwnPlaylist => (new SourceId("yandex"), "own", PlaylistCategory.Data),
            LegacyPlaylistType.YandexLikedPlaylist => (new SourceId("yandex"), "liked", PlaylistCategory.Data),
            LegacyPlaylistType.LocalFavorite => (LocalMusicSource.Id, "favorite", PlaylistCategory.Data),
            LegacyPlaylistType.LocalFolder => (LocalMusicSource.Id, "folder", PlaylistCategory.Data),
            LegacyPlaylistType.LocalArtist => (LocalMusicSource.Id, "artist", PlaylistCategory.Entity),
            LegacyPlaylistType.LocalAlbum => (LocalMusicSource.Id, "album", PlaylistCategory.Entity),
            LegacyPlaylistType.Artist => (null, "artist", PlaylistCategory.Entity),
            LegacyPlaylistType.Queue => (null, "queue", PlaylistCategory.Transient),
            LegacyPlaylistType.Cached => (null, "cached", PlaylistCategory.Computed),
            LegacyPlaylistType.Top10 => (null, "top10", PlaylistCategory.Computed),
            LegacyPlaylistType.TopEvenings => (null, "top-evenings", PlaylistCategory.Computed),
            LegacyPlaylistType.TopByDay => (null, "top-by-day", PlaylistCategory.Computed),
            LegacyPlaylistType.YandexSearch => (new SourceId("yandex"), "search", PlaylistCategory.Transient),
            LegacyPlaylistType.MyWave => (new SourceId("yandex"), "my-wave", PlaylistCategory.Transient),
            LegacyPlaylistType.LocalSearch => (LocalMusicSource.Id, "search", PlaylistCategory.Transient),
            _ => (null, "unknown", PlaylistCategory.Transient)
        };

    private static Playlist? ToPlaylist(JsonElement element)
    {
        string? typeName = GetString(element, "Type");
        if (typeName is null || !Enum.TryParse(typeName, ignoreCase: true, out LegacyPlaylistType legacyType))
        {
            Log.Warning("Снимок плейлистов: неизвестный вид плейлиста «{Type}», запись пропущена", typeName);
            return null;
        }

        (SourceId? sourceId, string kind, PlaylistCategory category) = MapLegacyType(legacyType);

        var playlist = new Playlist(kind, GetString(element, "Name") ?? string.Empty)
        {
            SourceId = sourceId,
            Category = category,
            Description = GetString(element, "Description") ?? string.Empty,
            TrackCount = GetInt(element, "TrackCount") ?? 0,
            Payload = ReadPayload(element)
        };

        return playlist;
    }

    private static IReadOnlyDictionary<string, string>? ReadPayload(JsonElement element)
    {
        if (!element.TryGetProperty("Payload", out JsonElement payload) || payload.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        int? folderId = GetInt(payload, "FolderId");
        if (folderId is not null)
            result["folder-id"] = folderId.Value.ToString();

        AddString("artist", GetString(payload, "ArtistName"));
        AddString("album", GetString(payload, "AlbumName"));
        AddString("owner", GetString(payload, "YandexOwnerUid"));
        AddString("kind", GetString(payload, "YandexKind"));

        return result.Count == 0 ? null : result;

        void AddString(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result[key] = value;
        }
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}
