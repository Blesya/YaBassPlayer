using YamBassPlayer.Enums;

namespace YamBassPlayer.Services;

public interface IPlaybackQueue
{
	event Action<string>? OnTrackChanged;
	bool LastChangeWasAutomatic { get; }
	string? CurrentTrackId { get; }
	bool HasNext { get; }
	bool HasPrevious { get; }
	string? PeekNextTrackId { get; }
	IReadOnlyList<string> TrackIds { get; }
	PlaybackMode Mode { get; set; }
	void SetQueue(IEnumerable<string> trackIds, int startIndex = 0);
	void AddToQueue(IEnumerable<string> trackIds);
	void Next();
	void NextAutomatic();
	void Previous();
	void Clear();
}