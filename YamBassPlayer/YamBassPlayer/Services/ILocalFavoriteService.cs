namespace YamBassPlayer.Services;

public interface ILocalFavoriteService
{
	bool IsTrackFavorite(string trackId);
	Task AddToFavorites(string trackId);
	Task RemoveFromFavorites(string trackId);
	Task<List<string>> GetAllFavoriteTrackIds();
	event Action<string>? OnFavoriteAdded;
	event Action<string>? OnFavoriteRemoved;
}
