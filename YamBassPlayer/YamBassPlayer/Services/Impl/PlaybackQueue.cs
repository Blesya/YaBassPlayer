using YamBassPlayer.Enums;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Services.Impl;

public class PlaybackQueue : IPlaybackQueue
{
	private readonly List<string> _trackIds = new();
	private readonly IAudioPlayer _audioPlayer;
	private readonly IEventBus? _eventBus;
	private readonly object _syncLock = new();
	private int _currentIndex = -1;
	private PlaybackMode _mode = PlaybackMode.Sequential;
	private readonly Random _random = new();
	private readonly Stack<int> _shuffleHistory = new();
	private int? _nextShuffleIndex = null;

	public event Action<string>? OnTrackChanged;

	public PlaybackQueue(IAudioPlayer audioPlayer, IEventBus? eventBus = null)
	{
		_audioPlayer = audioPlayer;
		_eventBus = eventBus;
		_audioPlayer.OnTrackEnded += OnTrackEnded;
	}

	public string? CurrentTrackId
	{
		get
		{
			lock (_syncLock)
				return _currentIndex >= 0 && _currentIndex < _trackIds.Count ? _trackIds[_currentIndex] : null;
		}
	}

	public bool HasNext
	{
		get
		{
			lock (_syncLock)
				return _mode == PlaybackMode.Shuffle ? _trackIds.Count > 0 : _currentIndex < _trackIds.Count - 1;
		}
	}

	public bool HasPrevious
	{
		get
		{
			lock (_syncLock)
				return _mode == PlaybackMode.Shuffle ? _shuffleHistory.Count > 0 : _currentIndex > 0;
		}
	}

	public string? PeekNextTrackId
	{
		get
		{
			lock (_syncLock)
			{
				if (_trackIds.Count == 0) return null;
				return _mode == PlaybackMode.Shuffle
					? _trackIds[EnsureShuffleNextLocked()]
					: HasNext ? _trackIds[_currentIndex + 1] : null;
			}
		}
	}

	public IReadOnlyList<string> TrackIds
	{
		get
		{
			lock (_syncLock)
				return _trackIds.ToList().AsReadOnly();
		}
	}

	public PlaybackMode Mode
	{
		get => _mode;
		set => _mode = value;
	}

	public void SetQueue(IEnumerable<string> trackIds, int startIndex = 0)
	{
		lock (_syncLock)
		{
			_trackIds.Clear();
			_trackIds.AddRange(trackIds);
			_currentIndex = startIndex;
			_shuffleHistory.Clear();
			_nextShuffleIndex = null;

			if (_currentIndex >= 0 && _currentIndex < _trackIds.Count)
			{
				RaiseTrackChanged(_trackIds[_currentIndex]);
			}
		}
	}

	public void AddToQueue(IEnumerable<string> trackIds)
	{
		lock (_syncLock)
			_trackIds.AddRange(trackIds);
	}

	public void Next()
	{
		lock (_syncLock)
		{
			if (_trackIds.Count == 0)
				return;

			if (_mode == PlaybackMode.Shuffle)
			{
				_shuffleHistory.Push(_currentIndex);
				_currentIndex = EnsureShuffleNextLocked();
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

			RaiseTrackChanged(_trackIds[_currentIndex]);
		}
	}

	public void Previous()
	{
		lock (_syncLock)
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

			RaiseTrackChanged(_trackIds[_currentIndex]);
		}
	}

	public void Clear()
	{
		lock (_syncLock)
		{
			_trackIds.Clear();
			_currentIndex = -1;
			_shuffleHistory.Clear();
			_nextShuffleIndex = null;
		}
	}

	private int EnsureShuffleNextLocked()
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

	private void RaiseTrackChanged(string trackId)
	{
		OnTrackChanged?.Invoke(trackId);
		_eventBus?.Publish(new TrackChangedEvent(trackId));
	}

	private void OnTrackEnded(object? sender, EventArgs e)
	{
		Next();
	}
}