namespace YamBassPlayer.Services;

public interface ILocalFavoriteService : ITrackFavoriteSourceService
{
	Task<List<string>> GetAllFavoriteTrackIds();
}
