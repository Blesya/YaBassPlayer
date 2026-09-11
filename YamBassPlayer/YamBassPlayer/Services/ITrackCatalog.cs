using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackCatalog
{
	Task<Track> GetAsync(string trackId);
	Task<IReadOnlyList<Track>> GetManyAsync(IEnumerable<string> trackIds);
	void Invalidate(string trackId);
	void Clear();
}
