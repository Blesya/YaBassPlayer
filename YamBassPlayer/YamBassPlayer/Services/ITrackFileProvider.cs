namespace YamBassPlayer.Services;

public interface ITrackFileProvider
{
	string GetTrackPath(string trackId);
	bool IsTrackDownloaded(string trackId);
	Task<string> DownloadTrackAsync(string trackId);
}