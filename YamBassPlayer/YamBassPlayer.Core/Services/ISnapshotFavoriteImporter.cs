namespace YamBassPlayer.Services;

/// <summary>
/// Переносит избранное из снимка плейлистов версии 1 в состояние источника.
/// </summary>
public interface ISnapshotFavoriteImporter
{
	void ImportIfMissing();
}
