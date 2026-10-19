using YamBassPlayer.Enums;

namespace YamBassPlayer.Models;

public class Playlist(string name, PlaylistType type)
{
	public string PlaylistName { get; } = name;

	public PlaylistType Type { get; } = type;

	public string Description { get; init; }

	public int TrackCount { get; set; }

	public DayOfWeek? DayOfWeek { get; init; }

	public string? SourceId { get; init; }

	public string? ParentTag { get; init; }

	/// <summary>Типизированные данные, определяющие поведение плейлиста (папка/альбом и т.п.).</summary>
	public PlaylistPayload? Payload { get; init; }

	/// <summary>
	/// Ключ кэша треков: у плейлистов Яндекс.Музыки — «владелец:kind», иначе имя.
	/// Нужен, чтобы одноимённые плейлисты разных владельцев не перетирали друг друга.
	/// </summary>
	public string TrackCacheKey =>
		Payload is { YandexOwnerUid: not null, YandexKind: not null } payload
			? $"{payload.YandexOwnerUid}:{payload.YandexKind}"
			: PlaylistName;

	public override string ToString()
	{
		return $"{PlaylistName} ({TrackCount})";
	}
}