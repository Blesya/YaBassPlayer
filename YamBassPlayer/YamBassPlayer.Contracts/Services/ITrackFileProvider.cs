using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface ITrackFileProvider
{
	string GetTrackPath(TrackKey key);
	bool IsTrackDownloaded(Track track);

	/// <summary>
	/// Downloads the track (unless it is already cached) and returns its local file path.
	/// <paramref name="progress"/> receives byte counts, plus the total size when it is known.
	/// </summary>
	Task<string> DownloadTrackAsync(Track track, IProgress<DownloadProgress>? progress = null);
}
