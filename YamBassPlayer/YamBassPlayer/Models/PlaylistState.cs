namespace YamBassPlayer.Models;

/// <summary>
/// Снимок состояния плейлистов, сохраняемый между запусками приложения.
/// Позволяет мгновенно показать дерево плейлистов и состав выбранного плейлиста,
/// пока актуальные данные асинхронно загружаются из источника.
/// </summary>
public sealed class PlaylistState
{
	/// <summary>Плейлисты, возвращённые <c>ITrackRepository.GetPlaylists</c> в прошлый раз.</summary>
	public List<Playlist> Playlists { get; init; } = [];

	/// <summary>Идентификаторы треков «Моих треков» (Яндекс.Музыка).</summary>
	public List<string> FavoriteTrackIds { get; init; } = [];

	/// <summary>Состав пользовательских плейлистов: имя плейлиста → идентификаторы треков.</summary>
	public Dictionary<string, List<string>> CustomPlaylistTrackIds { get; init; } = [];

	/// <summary>Плейлист, выбранный в прошлый раз (для восстановления выделения).</summary>
	public Playlist? LastPlaylist { get; init; }

	/// <summary>Момент сохранения снимка (UTC).</summary>
	public DateTime SavedAt { get; init; }
}
