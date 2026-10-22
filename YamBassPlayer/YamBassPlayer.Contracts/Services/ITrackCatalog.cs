using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackCatalog
{
	Task<Track> GetAsync(TrackKey key);
	Task<IReadOnlyList<Track>> GetManyAsync(IEnumerable<TrackKey> keys);
	void Invalidate(TrackKey key);
	void Clear();
}
