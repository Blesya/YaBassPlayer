namespace YamBassPlayer.Models;

/// <summary>
/// Типизированная полезная нагрузка плейлиста. Заменяет разбор служебных значений
/// из строки <see cref="Playlist.Description"/>, которая теперь свободна для отображения.
/// </summary>
public sealed record PlaylistPayload
{
	/// <summary>Идентификатор локальной папки (для <see cref="Enums.PlaylistType.LocalFolder"/>).</summary>
	public int? FolderId { get; init; }

	/// <summary>Имя исполнителя (для <c>LocalAlbum</c>); пустое — все исполнители.</summary>
	public string? ArtistName { get; init; }

	/// <summary>Название альбома (для <c>LocalAlbum</c>).</summary>
	public string? AlbumName { get; init; }
}
