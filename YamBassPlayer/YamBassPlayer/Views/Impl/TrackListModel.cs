using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Единственная копия состояния списка треков. Таблица и плитки — лишь два
/// способа отрисовать одни и те же <see cref="Items"/>.
/// </summary>
public sealed class TrackListModel : ITrackListModel
{
	private readonly List<TrackListItem> _allItems = [];
	private readonly List<TrackListItem> _items = [];
	private string? _filterText;

	public event Action? Changed;

	public IReadOnlyList<TrackListItem> Items => _items;
	public string? PlayingTrackId { get; private set; }
	public int SelectedIndex { get; private set; }
	public int ScrollOffset { get; private set; }
	public bool IsLoadingMore { get; private set; }

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		_allItems.Clear();
		int number = 0;
		foreach (Track track in tracks)
		{
			number++;
			_allItems.Add(new TrackListItem(track, PadNumber(number, isCached(track.Id))));
		}

		ApplyFilter();
		SelectedIndex = 0;
		ScrollOffset = 0;
		IsLoadingMore = false;
		Changed?.Invoke();
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		int number = _allItems.Count;
		foreach (Track track in tracks)
		{
			number++;
			_allItems.Add(new TrackListItem(track, PadNumber(number, isCached(track.Id))));
		}

		ApplyFilter();
		IsLoadingMore = false;
		Changed?.Invoke();
	}

	public void Clear()
	{
		_allItems.Clear();
		_items.Clear();
		SelectedIndex = 0;
		ScrollOffset = 0;
		Changed?.Invoke();
	}

	public void SetFilter(string? filter)
	{
		_filterText = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
		ApplyFilter();
		SelectedIndex = 0;
		ScrollOffset = 0;
		Changed?.Invoke();
	}

	public void SetPlayingTrackId(string? trackId)
	{
		PlayingTrackId = trackId;
		Changed?.Invoke();
	}

	public void SetLoadingMore(bool value)
	{
		if (IsLoadingMore == value)
			return;

		IsLoadingMore = value;
		Changed?.Invoke();
	}

	public bool MoveUp(int step)
	{
		if (step <= 0)
			return false;

		return MoveSelectionTo(SelectedIndex - step);
	}

	public bool MoveDown(int step)
	{
		if (step <= 0)
			return false;

		return MoveSelectionTo(SelectedIndex + step);
	}

	public bool PageUp(int pageSize) => MoveUp(Math.Max(1, pageSize));

	public bool PageDown(int pageSize) => MoveDown(Math.Max(1, pageSize));

	public bool Home() => MoveSelectionTo(0);

	public bool End() => MoveSelectionTo(_items.Count - 1);

	public bool Select(int index) => MoveSelectionTo(index);

	public bool ScrollBy(int delta, int visibleCount)
	{
		if (delta == 0)
			return false;

		int max = Math.Max(0, _items.Count - Math.Max(1, visibleCount));
		int next = Math.Clamp(ScrollOffset + delta, 0, max);
		if (next == ScrollOffset)
			return false;

		ScrollOffset = next;
		Changed?.Invoke();
		return true;
	}

	public void EnsureSelectedVisible(int visibleCount, int stride = 1)
	{
		if (_items.Count == 0)
			return;

		int s = Math.Max(1, stride);
		int vis = Math.Max(1, visibleCount);
		int selectedRow = SelectedIndex / s;
		int visibleRows = Math.Max(1, (vis + s - 1) / s);
		int scrollRow = ScrollOffset / s;

		if (selectedRow < scrollRow)
			scrollRow = selectedRow;
		else if (selectedRow >= scrollRow + visibleRows)
			scrollRow = selectedRow - visibleRows + 1;

		int next = scrollRow * s;
		if (next == ScrollOffset)
			return;

		ScrollOffset = next;
		Changed?.Invoke();
	}

	public bool ShouldRequestMore(int visibleCount)
	{
		if (_items.Count == 0 || IsLoadingMore)
			return false;

		int vis = Math.Max(1, visibleCount);
		if (ScrollOffset + vis >= _items.Count - 10)
		{
			IsLoadingMore = true;
			return true;
		}

		return false;
	}

	private bool MoveSelectionTo(int index)
	{
		if (_items.Count == 0)
			return false;

		int next = Math.Clamp(index, 0, _items.Count - 1);
		if (next == SelectedIndex)
			return false;

		SelectedIndex = next;
		Changed?.Invoke();
		return true;
	}

	private void ApplyFilter()
	{
		_items.Clear();
		if (_filterText == null)
		{
			_items.AddRange(_allItems);
			return;
		}

		string f = _filterText;
		_items.AddRange(_allItems.Where(i =>
			(i.Track.Artist ?? "").Contains(f, StringComparison.OrdinalIgnoreCase) ||
			(i.Track.Title ?? "").Contains(f, StringComparison.OrdinalIgnoreCase)));
	}

	private static string PadNumber(int n, bool isCached)
		=> n.ToString().PadLeft(2) + (isCached ? "*" : " ");
}
