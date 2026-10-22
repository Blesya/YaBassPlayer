using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IPlaybackQueue
{
	event Action<TrackKey>? OnTrackChanged;
	bool LastChangeWasAutomatic { get; }
	TrackKey? CurrentTrack { get; }
	bool HasNext { get; }
	bool HasPrevious { get; }
	TrackKey? PeekNext { get; }
	IReadOnlyList<TrackKey> TrackKeys { get; }
	PlaybackMode Mode { get; set; }
	void SetQueue(IEnumerable<TrackKey> trackKeys, int startIndex = 0);
	void AddToQueue(IEnumerable<TrackKey> trackKeys);
	void Next();
	void NextAutomatic();
	void Previous();
	void Clear();
}
