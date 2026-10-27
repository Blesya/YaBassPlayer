namespace YamBassPlayer.Enums;

/// <summary>
/// Что обновлять в фоне. <see cref="AppPlaylists"/> — только локальные плейлисты
/// приложения (избранное, «Загруженные», топы), без обращения к сети.
/// </summary>
public enum PlaylistRefreshScope
{
	None,
	AppPlaylists,
	All
}
