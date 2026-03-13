namespace YamBassPlayer.Services;

public interface ICoverProvider
{
	string GetCoverPath(string trackId);
	bool IsCoverDownloaded(string trackId);
	Task<string> DownloadCoverAsync(string trackId);
}
