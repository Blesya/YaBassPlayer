namespace YamBassPlayer.Services;

public interface IYandexFavoriteService
{
	void Initialize(IEnumerable<string> likedIds);
	bool IsTrackFavorite(string trackId);
	Task AddToFavorites(string trackId);
	Task RemoveFromFavorites(string trackId);
	event Action<string>? OnFavoriteAdded;
	event Action<string>? OnFavoriteRemoved;
}
