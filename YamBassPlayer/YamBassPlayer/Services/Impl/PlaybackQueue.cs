namespace YamBassPlayer.Services.Impl;

public class PlaybackQueue : IPlaybackQueue
{
	private readonly List<string> _trackIds = new();
	private readonly IAudioPlayer _audioPlayer;
	private int _currentIndex = -1;

	public event Action<string>? OnTrackChanged;

	public PlaybackQueue(IAudioPlayer audioPlayer)
	{
		_audioPlayer = audioPlayer;
		_audioPlayer.OnTrackEnded += OnTrackEnded;
	}

	public string? CurrentTrackId => _currentIndex >= 0 && _currentIndex < _trackIds.Count 
		? _trackIds[_currentIndex] 
		: null;

	public bool HasNext => _currentIndex < _trackIds.Count - 1;
	public bool HasPrevious => _currentIndex > 0;
	public string? PeekNextTrackId => HasNext ? _trackIds[_currentIndex + 1] : null;

	public void SetQueue(IEnumerable<string> trackIds, int startIndex = 0)
	{
		_trackIds.Clear();
		_trackIds.AddRange(trackIds);
		_currentIndex = startIndex;

		if (_currentIndex >= 0 && _currentIndex < _trackIds.Count)
		{
			OnTrackChanged?.Invoke(_trackIds[_currentIndex]);
		}
	}

	public void AddToQueue(IEnumerable<string> trackIds)
	{
		_trackIds.AddRange(trackIds);
	}

	public void Next()
	{
		if (_trackIds.Count == 0)
			return;

		if (!HasNext)
		{
			_currentIndex = 0;
		}
		else
		{
			_currentIndex++;
		}

		OnTrackChanged?.Invoke(_trackIds[_currentIndex]);
	}

	public void Previous()
	{
		if (_trackIds.Count == 0)
			return;

		if (!HasPrevious)
		{
			_currentIndex = _trackIds.Count - 1;
		}
		else
		{
			_currentIndex--;
		}

		OnTrackChanged?.Invoke(_trackIds[_currentIndex]);
	}

	public void Clear()
	{
		_trackIds.Clear();
		_currentIndex = -1;
	}

	private void OnTrackEnded(object? sender, EventArgs e)
	{
		Next();
	}
}