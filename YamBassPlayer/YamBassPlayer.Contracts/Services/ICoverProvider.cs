using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ICoverProvider
{
	string GetCoverPath(TrackKey key);
	bool IsCoverDownloaded(TrackKey key);
	Task<string> DownloadCoverAsync(Track track);
}
