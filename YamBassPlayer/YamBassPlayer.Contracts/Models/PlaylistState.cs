namespace YamBassPlayer.Models;

/// <summary>
/// Снимок состояния плейлистов, сохраняемый между запусками приложения.
/// Позволяет мгновенно показать дерево плейлистов и состав выбранного плейлиста,
/// пока актуальные данные асинхронно загружаются из источника.
/// </summary>
public sealed class PlaylistState
{
	/// <summary>Плейлисты категории <c>Data</c>, возвращённые <c>ITrackRepository.GetPlaylists</c> в прошлый раз.</summary>
	public List<Playlist> Playlists { get; init; } = [];

	/// <summary>Плейлист, выбранный в прошлый раз (для восстановления выделения); любой, кроме временного.</summary>
	public Playlist? LastPlaylist { get; init; }

	/// <summary>Трек, который играл последним (для восстановления выделения в списке).</summary>
	public TrackKey? LastTrack { get; init; }

	/// <summary>Момент сохранения снимка (UTC).</summary>
	public DateTime SavedAt { get; init; }
}
