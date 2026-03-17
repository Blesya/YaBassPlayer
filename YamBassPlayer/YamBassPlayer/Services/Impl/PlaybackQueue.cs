using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Impl;

public class PlaybackQueue : IPlaybackQueue
{
	private readonly List<string> _trackIds = new();
	private readonly IAudioPlayer _audioPlayer;
	private int _currentIndex = -1;
	private PlaybackMode _mode = PlaybackMode.Sequential;
	private readonly Random _random = new();
	private readonly Stack<int> _shuffleHistory = new();
	private int? _nextShuffleIndex = null;

	public event Action<string>? OnTrackChanged;

	public PlaybackQueue(IAudioPlayer audioPlayer)
	{
		_audioPlayer = audioPlayer;
		_audioPlayer.OnTrackEnded += OnTrackEnded;
	}

	public string? CurrentTrackId => _currentIndex >= 0 && _currentIndex < _trackIds.Count 
		? _trackIds[_currentIndex] 
		: null;

	public bool HasNext => _mode == PlaybackMode.Shuffle
		? _trackIds.Count > 0
		: _currentIndex < _trackIds.Count - 1;

	public bool HasPrevious => _mode == PlaybackMode.Shuffle
		? _shuffleHistory.Count > 0
		: _currentIndex > 0;

	public string? PeekNextTrackId => _trackIds.Count == 0
		? null
		: _mode == PlaybackMode.Shuffle
			? _trackIds[EnsureShuffleNext()]
			: HasNext ? _trackIds[_currentIndex + 1] : null;

	public IReadOnlyList<string> TrackIds => _trackIds.AsReadOnly();

	public PlaybackMode Mode
	{
		get => _mode;
		set => _mode = value;
	}

	public void SetQueue(IEnumerable<string> trackIds, int startIndex = 0)
	{
		_trackIds.Clear();
		_trackIds.AddRange(trackIds);
		_currentIndex = startIndex;
		_shuffleHistory.Clear();
		_nextShuffleIndex = null;

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

		if (_mode == PlaybackMode.Shuffle)
		{
			_shuffleHistory.Push(_currentIndex);
			_currentIndex = EnsureShuffleNext();
			_nextShuffleIndex = null;
		}
		else
		{
			if (!HasNext)
			{
				_currentIndex = 0;
			}
			else
			{
				_currentIndex++;
			}
		}

		OnTrackChanged?.Invoke(_trackIds[_currentIndex]);
	}

	public void Previous()
	{
		if (_trackIds.Count == 0)
			return;

		if (_mode == PlaybackMode.Shuffle && _shuffleHistory.Count > 0)
		{
			_currentIndex = _shuffleHistory.Pop();
			_nextShuffleIndex = null;
		}
		else
		{
			if (!HasPrevious)
			{
				_currentIndex = _trackIds.Count - 1;
			}
			else
			{
				_currentIndex--;
			}
		}

		OnTrackChanged?.Invoke(_trackIds[_currentIndex]);
	}

	public void Clear()
	{
		_trackIds.Clear();
		_currentIndex = -1;
		_shuffleHistory.Clear();
		_nextShuffleIndex = null;
	}

	private int EnsureShuffleNext()
	{
		if (_nextShuffleIndex == null)
		{
			if (_trackIds.Count == 1)
			{
				_nextShuffleIndex = 0;
			}
			else
			{
				int next;
				do
				{
					next = _random.Next(0, _trackIds.Count);
				} while (next == _currentIndex);
				_nextShuffleIndex = next;
			}
		}

		return _nextShuffleIndex.Value;
	}

	private void OnTrackEnded(object? sender, EventArgs e)
	{
		Next();
	}
}