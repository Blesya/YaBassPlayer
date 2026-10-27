namespace YamBassPlayer.Models;

/// <summary>
/// Состояние, которое можно показать до обращения к сети: плейлисты предыдущего
/// запуска (включая ветки источников, которые ещё не готовы) и локально вычислимые
/// плейлисты приложения. Источник истины для первого кадра окна.
/// </summary>
public sealed record PlaylistStartupSnapshot(
	IReadOnlyList<Playlist> Playlists,
	Playlist? LastPlaylist,
	TrackKey? LastTrack,
	DateTime SavedAt)
{
	public static PlaylistStartupSnapshot Empty { get; } = new([], null, null, default);
}
