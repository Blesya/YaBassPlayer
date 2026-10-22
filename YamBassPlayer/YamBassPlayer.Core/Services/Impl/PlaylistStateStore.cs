using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// JSON-хранилище снимка плейлистов (<c>playlist_state.json</c>).
/// Чтение и запись синхронные: файл читается один раз при старте, а состояние
/// нужно показать немедленно, поэтому асинхронность здесь не даёт выигрыша.
/// Любые ошибки чтения/записи проглатываются — кэш не критичен для работы.
/// </summary>
public sealed class PlaylistStateStore(string filePath, IPluginStateFile pluginState) : IPlaylistStateStore
{
	private const int CurrentVersion = 2;
	private const string YandexPluginId = "yandex-music";
	private const string FavoriteTrackIdsKey = "favoriteTrackIds";
	private const string CustomPlaylistIdsKey = "customPlaylistIds";

	private readonly object _sync = new();

	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	public PlaylistState? Load()
	{
		Logging.LogBeforeCall();

		lock (_sync)
		{
			try
			{
				if (!File.Exists(filePath))
					return null;

				string json = File.ReadAllText(filePath);
				using var document = JsonDocument.Parse(json);

				if (!document.RootElement.TryGetProperty("Version", out JsonElement versionElement)
					|| versionElement.ValueKind != JsonValueKind.Number)
				{
					Log.Warning("Снимок плейлистов: не найдена версия формата");
					return null;
				}

				int version = versionElement.GetInt32();
				if (version == 1)
				{
					PlaylistState? migrated = MigrateFromVersionOne(document);
					Logging.LogAfterCall();
					return migrated;
				}

				if (version != CurrentVersion)
				{
					Log.Warning("Снимок плейлистов: неизвестная версия {Version}", version);
					return null;
				}

				var dto = JsonSerializer.Deserialize<StateDto>(json, Options);
				if (dto is null)
					return null;

				Logging.LogAfterCall();
				return new PlaylistState
				{
					Playlists = dto.Playlists.Select(ToPlaylist).Select(NormalizeLegacyLocalFavorite).ToList(),
					LastPlaylist = dto.LastPlaylist is null
						? null
						: NormalizeLegacyLocalFavorite(ToPlaylist(dto.LastPlaylist)),
					LastTrack = dto.LastTrack is null
						? null
						: new TrackKey(new SourceId(dto.LastTrack.SourceId), dto.LastTrack.TrackId),
					SavedAt = dto.SavedAt
				};
			}
			catch
			{
				// Повреждённый или несовместимый файл не должен ломать запуск.
				return null;
			}
		}
	}

	public void Save(PlaylistState state)
	{
		Logging.LogBeforeCall();

		lock (_sync)
		{
			try
			{
				var dto = new StateDto
				{
					Version = CurrentVersion,
					SavedAt = state.SavedAt == default ? DateTime.UtcNow : state.SavedAt,
					Playlists = state.Playlists
						.Where(p => p.Category == PlaylistCategory.Data)
						.Select(FromPlaylist)
						.ToList(),
					LastPlaylist = state.LastPlaylist is { Category: not PlaylistCategory.Transient } last
						? FromPlaylist(last)
						: null,
					LastTrack = state.LastTrack is { } track
						? new TrackKeyDto { SourceId = track.SourceId.Value, TrackId = track.TrackId }
						: null
				};

				var directory = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);

				File.WriteAllText(filePath, JsonSerializer.Serialize(dto, Options));
			}
			catch
			{
				// Ошибка записи кэша не критична для работы приложения.
			}
		}

		Logging.LogAfterCall();
	}

	private PlaylistState? MigrateFromVersionOne(JsonDocument document)
	{
		if (!PlaylistSnapshotMigration.TryMigrate(document, out MigratedSnapshot? snapshot) || snapshot is null)
			return null;

		string? directory = Path.GetDirectoryName(filePath);
		string backupPath = Path.Combine(directory ?? string.Empty, "playlist_state.v1.json");
		try
		{
			if (!File.Exists(backupPath))
				File.Copy(filePath, backupPath, overwrite: false);
		}
		catch (Exception ex)
		{
			Log.Warning(ex, "Не удалось сохранить копию снимка плейлистов версии 1");
		}

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

		Log.Information("Снимок плейлистов мигрирован с версии 1 на {Version}", CurrentVersion);
		PlaylistState normalized = new()
		{
			Playlists = snapshot.State.Playlists.Select(NormalizeLegacyLocalFavorite).ToList(),
			LastPlaylist = snapshot.State.LastPlaylist is null
				? null
				: NormalizeLegacyLocalFavorite(snapshot.State.LastPlaylist),
			LastTrack = snapshot.State.LastTrack,
			SavedAt = snapshot.State.SavedAt
		};
		Save(normalized);
		return normalized;
	}

	private static Playlist ToPlaylist(PlaylistDto dto) =>
		new(dto.Kind, dto.Title)
		{
			SourceId = string.IsNullOrWhiteSpace(dto.SourceId) ? null : new SourceId(dto.SourceId),
			Description = dto.Description ?? string.Empty,
			TrackCount = dto.TrackCount,
			Category = dto.Category,
			Context = dto.Context,
			Group = dto.Group,
			Payload = dto.Payload,
			CacheKey = dto.CacheKey ?? string.Empty
		};

	/// <summary>
	/// Старое локальное избранное было плейлистом источника <c>local</c>,
	/// теперь это корневой «Локальное избранное». Иначе прошлый выбор не найдётся.
	/// </summary>
	private static Playlist NormalizeLegacyLocalFavorite(Playlist playlist)
	{
		if (playlist.SourceId != LocalMusicSource.Id || playlist.Kind != "favorite")
			return playlist;

		return new Playlist(playlist.Kind, "Локальное избранное")
		{
			SourceId = null,
			Description = playlist.Description,
			TrackCount = playlist.TrackCount,
			Category = playlist.Category,
			Context = playlist.Context,
			Group = playlist.Group,
			Payload = playlist.Payload
		};
	}

	private static PlaylistDto FromPlaylist(Playlist playlist) =>
		new()
		{
			SourceId = playlist.SourceId?.Value,
			Kind = playlist.Kind,
			Title = playlist.Title,
			Description = playlist.Description,
			TrackCount = playlist.TrackCount,
			Category = playlist.Category,
			Context = playlist.Context,
			Group = playlist.Group,
			Payload = playlist.Payload is null
				? null
				: new Dictionary<string, string>(playlist.Payload),
			CacheKey = playlist.CacheKey
		};

	private sealed class StateDto
	{
		public int Version { get; set; }
		public DateTime SavedAt { get; set; }
		public List<PlaylistDto> Playlists { get; set; } = [];
		public PlaylistDto? LastPlaylist { get; set; }
		public TrackKeyDto? LastTrack { get; set; }
	}

	private sealed class PlaylistDto
	{
		public string? SourceId { get; set; }
		public string Kind { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string? Description { get; set; }
		public int TrackCount { get; set; }
		public PlaylistCategory Category { get; set; } = PlaylistCategory.Data;
		public PlaylistContext Context { get; set; } = PlaylistContext.Regular;
		public string? Group { get; set; }
		public Dictionary<string, string>? Payload { get; set; }
		public string? CacheKey { get; set; }
	}

	private sealed class TrackKeyDto
	{
		public string SourceId { get; set; } = string.Empty;
		public string TrackId { get; set; } = string.Empty;
	}
}
