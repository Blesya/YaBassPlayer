using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Единое избранное, которое хранится локально в состоянии плагина
/// (<c>plugin_state/local.json</c>). Пользователь сам добавляет туда треки
/// любого источника; это не лайки Яндекс.Музыки.
/// </summary>
public interface ILocalFavoriteService
{
	bool IsTrackFavorite(TrackKey key);

	Task AddToFavorites(TrackKey key);

	Task RemoveFromFavorites(TrackKey key);

	/// <summary>Все избранные треки, новые сверху.</summary>
	Task<IReadOnlyList<TrackKey>> GetAllFavoriteKeys();

	event Action? Changed;
}
