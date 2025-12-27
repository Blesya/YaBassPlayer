namespace YamBassPlayer.Services;

public interface IHistoryService
{
    void LogListen(string trackId);
    IReadOnlyList<(string trackId, int count)> GetTopTracks(int limit = 10);
    IReadOnlyList<(string trackId, int count)> GetTopEveningTracks(int limit = 10);
}