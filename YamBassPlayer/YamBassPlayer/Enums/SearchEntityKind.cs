namespace YamBassPlayer.Enums;

/// <summary>
/// Режим поиска: что именно возвращать по запросу.
/// </summary>
public enum SearchEntityKind
{
	/// <summary>Выдача из найденных треков.</summary>
	Tracks,

	/// <summary>Выдача треков первого найденного исполнителя.</summary>
	Artist,

	/// <summary>Выдача треков первого найденного альбома.</summary>
	Album
}
