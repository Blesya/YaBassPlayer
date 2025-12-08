namespace YamBassPlayer.Services;

public interface IPlaybackQueue
{
    event Action<string>? OnTrackChanged;
    string? CurrentTrackId { get; }
    bool HasNext { get; }
    bool HasPrevious { get; }
    void SetQueue(IEnumerable<string> trackIds, int startIndex = 0);
    void AddToQueue(IEnumerable<string> trackIds);
    void Next();
    void Previous();
    void Clear();
}