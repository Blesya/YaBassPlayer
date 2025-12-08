using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackInfoProvider
{
    Task<IEnumerable<Track>> GetTracksInfoByIds(IEnumerable<string> ids);
    Task<Track> GetTrackInfoById(string id);
    Task SaveAsync(Track track);
    Task<bool> IsTrackCached(string trackId);
    Task<int> CountCachedTracks(IEnumerable<string> trackIds);
}