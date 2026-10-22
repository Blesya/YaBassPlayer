namespace YamBassPlayer.Services;

public interface IYandexFavoriteService : ITrackFavoriteSourceService
{
	void Initialize(IEnumerable<string> likedIds);
}
