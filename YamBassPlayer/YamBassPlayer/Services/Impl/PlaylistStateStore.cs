using System.Text.Json;
using System.Text.Json.Serialization;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// JSON-хранилище снимка плейлистов (<c>playlist_state.json</c>).
/// Чтение и запись синхронные: файл читается один раз при старте, а состояние
/// нужно показать немедленно, поэтому асинхронность здесь не даёт выигрыша.
/// Любые ошибки чтения/записи проглатываются — кэш не критичен для работы.
/// </summary>
public sealed class PlaylistStateStore(string filePath) : IPlaylistStateStore
{
	private const int CurrentVersion = 1;

	private readonly object _sync = new();

	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	public PlaylistState? Load()
	{
		lock (_sync)
		{
			try
			{
				if (!File.Exists(filePath))
					return null;

				var dto = JsonSerializer.Deserialize<StateDto>(File.ReadAllText(filePath), Options);
				if (dto is null || dto.Version != CurrentVersion)
					return null;

				return new PlaylistState
				{
					Playlists = dto.Playlists.Select(ToPlaylist).ToList(),
					FavoriteTrackIds = dto.FavoriteTrackIds ?? [],
					CustomPlaylistTrackIds = dto.CustomPlaylistTrackIds ?? [],
					LastPlaylist = dto.LastPlaylist is null ? null : ToPlaylist(dto.LastPlaylist),
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
		lock (_sync)
		{
			try
			{
				var dto = new StateDto
				{
					Version = CurrentVersion,
					SavedAt = state.SavedAt == default ? DateTime.UtcNow : state.SavedAt,
					Playlists = state.Playlists.Select(FromPlaylist).ToList(),
					FavoriteTrackIds = state.FavoriteTrackIds.ToList(),
					CustomPlaylistTrackIds = state.CustomPlaylistTrackIds
						.ToDictionary(pair => pair.Key, pair => pair.Value),
					LastPlaylist = state.LastPlaylist is null ? null : FromPlaylist(state.LastPlaylist)
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
	}

	private static Playlist ToPlaylist(PlaylistDto dto) =>
		new(dto.Name, dto.Type)
		{
			Description = dto.Description ?? string.Empty,
			TrackCount = dto.TrackCount,
			DayOfWeek = dto.Weekday,
			SourceId = dto.SourceId,
			ParentTag = dto.ParentTag,
			Payload = dto.Payload is null ? null : new PlaylistPayload
			{
				FolderId = dto.Payload.FolderId,
				ArtistName = dto.Payload.ArtistName,
				AlbumName = dto.Payload.AlbumName
			}
		};

	private static PlaylistDto FromPlaylist(Playlist playlist) =>
		new()
		{
			Name = playlist.PlaylistName,
			Type = playlist.Type,
			Description = playlist.Description,
			TrackCount = playlist.TrackCount,
			Weekday = playlist.DayOfWeek,
			SourceId = playlist.SourceId,
			ParentTag = playlist.ParentTag,
			Payload = playlist.Payload is null ? null : new PayloadDto
			{
				FolderId = playlist.Payload.FolderId,
				ArtistName = playlist.Payload.ArtistName,
				AlbumName = playlist.Payload.AlbumName
			}
		};

	private sealed class StateDto
	{
		public int Version { get; set; }
		public DateTime SavedAt { get; set; }
		public List<PlaylistDto> Playlists { get; set; } = [];
		public List<string> FavoriteTrackIds { get; set; } = [];
		public Dictionary<string, List<string>> CustomPlaylistTrackIds { get; set; } = [];
		public PlaylistDto? LastPlaylist { get; set; }
	}

	private sealed class PlaylistDto
	{
		public string Name { get; set; } = string.Empty;
		public PlaylistType Type { get; set; }
		public string? Description { get; set; }
		public int TrackCount { get; set; }
		public DayOfWeek? Weekday { get; set; }
		public string? SourceId { get; set; }
		public string? ParentTag { get; set; }
		public PayloadDto? Payload { get; set; }
	}

	private sealed class PayloadDto
	{
		public int? FolderId { get; set; }
		public string? ArtistName { get; set; }
		public string? AlbumName { get; set; }
	}
}
