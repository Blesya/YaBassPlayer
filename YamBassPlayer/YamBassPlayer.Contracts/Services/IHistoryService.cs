using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IHistoryService
{
	void LogListen(TrackKey key, PlaylistContext context);
	IReadOnlyList<(TrackKey key, int count)> GetTopTracks(int limit = 10);
	IReadOnlyList<(TrackKey key, int count)> GetTopEveningTracks(int limit = 10);
	IReadOnlyList<(TrackKey key, int count)> GetTopTracksByDayOfWeek(DayOfWeek day, int limit = 50);
	int GetListenCount(TrackKey key);
}
