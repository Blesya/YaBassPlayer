using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Services.Impl;

public class PlaybackQueue : IPlaybackQueue
{
	private readonly List<TrackKey> _trackKeys = new();
	private readonly IAudioPlayer _audioPlayer;
	private readonly IEventBus? _eventBus;
	private readonly object _syncLock = new();
	private int _currentIndex = -1;
	private PlaybackMode _mode = PlaybackMode.Sequential;
	private readonly Random _random = new();
	private readonly Stack<int> _shuffleHistory = new();
	private int? _nextShuffleIndex = null;
	private bool _lastChangeWasAutomatic;

	public event Action<TrackKey>? OnTrackChanged;

	public bool LastChangeWasAutomatic => _lastChangeWasAutomatic;

	public PlaybackQueue(IAudioPlayer audioPlayer, IEventBus? eventBus = null)
	{
		_audioPlayer = audioPlayer;
		_eventBus = eventBus;
		_audioPlayer.OnTrackEnded += OnTrackEnded;
	}

	public TrackKey? CurrentTrack
	{
		get
		{
			lock (_syncLock)
				return _currentIndex >= 0 && _currentIndex < _trackKeys.Count ? _trackKeys[_currentIndex] : null;
		}
	}

	public bool HasNext
	{
		get
		{
			lock (_syncLock)
				return _mode == PlaybackMode.Shuffle ? _trackKeys.Count > 0 : _currentIndex < _trackKeys.Count - 1;
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

	public TrackKey? PeekNext
	{
		get
		{
			lock (_syncLock)
			{
				if (_trackKeys.Count == 0 || _mode == PlaybackMode.Repeat) return null;
				return _mode == PlaybackMode.Shuffle
					? _trackKeys[EnsureShuffleNextLocked()]
					: HasNext ? _trackKeys[_currentIndex + 1] : null;
			}
		}
	}

	public IReadOnlyList<TrackKey> TrackKeys
	{
		get
		{
			lock (_syncLock)
				return _trackKeys.ToList().AsReadOnly();
		}
	}

	public PlaybackMode Mode
	{
		get => _mode;
		set => _mode = value;
	}

	public void SetQueue(IEnumerable<TrackKey> trackKeys, int startIndex = 0)
	{
		Logging.LogBeforeCall();

		lock (_syncLock)
		{
			_trackKeys.Clear();
			_trackKeys.AddRange(trackKeys);
			_currentIndex = startIndex;
			_shuffleHistory.Clear();
			_nextShuffleIndex = null;

			if (_currentIndex >= 0 && _currentIndex < _trackKeys.Count)
			{
				_lastChangeWasAutomatic = false;
				RaiseTrackChanged(_trackKeys[_currentIndex]);
			}
		}

		Logging.LogAfterCall();
	}

	public void AddToQueue(IEnumerable<TrackKey> trackKeys)
	{
		lock (_syncLock)
			_trackKeys.AddRange(trackKeys);
	}

	public void Next() => Advance(automatic: false);

	public void NextAutomatic() => Advance(automatic: true);

	private void Advance(bool automatic)
	{
		Logging.LogBeforeCall();

		lock (_syncLock)
		{
			if (_trackKeys.Count == 0)
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

			_lastChangeWasAutomatic = automatic;
			RaiseTrackChanged(_trackKeys[_currentIndex]);
		}

		Logging.LogAfterCall();
	}

	public void Previous()
	{
		Logging.LogBeforeCall();

		lock (_syncLock)
		{
			if (_trackKeys.Count == 0)
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
					_currentIndex = _trackKeys.Count - 1;
				}
				else
				{
					_currentIndex--;
				}
			}

			_lastChangeWasAutomatic = false;
			RaiseTrackChanged(_trackKeys[_currentIndex]);
		}

		Logging.LogAfterCall();
	}

	public void Clear()
	{
		Logging.LogBeforeCall();

		lock (_syncLock)
		{
			_trackKeys.Clear();
			_currentIndex = -1;
			_shuffleHistory.Clear();
			_nextShuffleIndex = null;
		}

		Logging.LogAfterCall();
	}

	private int EnsureShuffleNextLocked()
	{
		if (_nextShuffleIndex == null)
		{
			if (_trackKeys.Count == 1)
			{
				_nextShuffleIndex = 0;
			}
			else
			{
				int next;
				do
				{
					next = _random.Next(0, _trackKeys.Count);
				} while (next == _currentIndex);
				_nextShuffleIndex = next;
			}
		}

		return _nextShuffleIndex.Value;
	}

	private void RaiseTrackChanged(TrackKey key)
	{
		OnTrackChanged?.Invoke(key);
		_eventBus?.Publish(new TrackChangedEvent(key));
	}

	private void OnTrackEnded(object? sender, EventArgs e)
	{
		// Повтор: тот же трек стартует заново, очередь не двигается.
		if (_mode == PlaybackMode.Repeat)
		{
			lock (_syncLock)
			{
				if (_currentIndex >= 0 && _currentIndex < _trackKeys.Count)
				{
					_lastChangeWasAutomatic = true;
					RaiseTrackChanged(_trackKeys[_currentIndex]);
				}
			}

			return;
		}

		NextAutomatic();
	}
}
