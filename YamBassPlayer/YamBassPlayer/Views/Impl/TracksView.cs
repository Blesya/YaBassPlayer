using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Список треков в одну колонку. Каждая строка: «№  Исполнитель — Название».
/// Текст выбранной строки бежит строкой (marquee), если не помещается по ширине.
/// </summary>
public sealed class TracksView : View, ITracksView
{
	private sealed record RowData(Track Track, string Number);

	private const int MarqueeIntervalMs = 250;
	private const int MarqueePauseTicks = 4;

	private readonly List<RowData> _allRows = [];
	private readonly List<RowData> _rows = [];

	private string? _filterText;
	private string? _playingTrackId;
	private int _selectedIndex;
	private int _scrollOffset;
	private bool _isLoadingMore;

	private object? _marqueeToken;
	private int _marqueeOffset;
	private int _marqueePause;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksView()
	{
		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		var list = tracks.ToList();
		Application.MainLoop.Invoke(() =>
		{
			_allRows.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				Track t = list[i];
				string number = PadNumber(i + 1, isCached(t.Id));
				_allRows.Add(new RowData(t, number));
			}

			ApplyFilter();
			_selectedIndex = 0;
			_scrollOffset = 0;
			_isLoadingMore = false;
			ResetMarquee();
			SetNeedsDisplay();
		});
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		var incoming = new List<RowData>();
		foreach (Track t in tracks)
		{
			string number = PadNumber(_allRows.Count + incoming.Count + 1, isCached(t.Id));
			incoming.Add(new RowData(t, number));
		}

		Application.MainLoop.Invoke(() =>
		{
			_allRows.AddRange(incoming);
			ApplyFilter();
			_isLoadingMore = false;
			ResetMarquee();
			SetNeedsDisplay();
		});
	}

	public void ClearTracks()
	{
		Application.MainLoop.Invoke(() =>
		{
			StopMarquee();
			_allRows.Clear();
			_rows.Clear();
			_selectedIndex = 0;
			_scrollOffset = 0;
			SetNeedsDisplay();
		});
	}

	public void SetPlayingTrackId(string? trackId)
	{
		Application.MainLoop.Invoke(() =>
		{
			_playingTrackId = trackId;
			ResetMarquee();
			SetNeedsDisplay();
		});
	}

	public void SetFilter(string? filter)
	{
		_filterText = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
		_selectedIndex = 0;
		_scrollOffset = 0;
		ApplyFilter();
		ResetMarquee();
		SetNeedsDisplay();
	}

	private void ApplyFilter()
	{
		_rows.Clear();
		if (_filterText == null)
		{
			_rows.AddRange(_allRows);
			return;
		}

		string f = _filterText.ToLower();
		_rows.AddRange(_allRows.Where(r =>
			(r.Track.Artist ?? "").ToLower().Contains(f) ||
			(r.Track.Title ?? "").ToLower().Contains(f)));
	}

	private static string PadNumber(int n, bool isCached)
		=> n.ToString().PadLeft(2) + (isCached ? "*" : " ");

	private string RowText(RowData r)
	{
		string artist = r.Track.Artist ?? "";
		string title = r.Track.Title ?? "";
		string artistTitle = string.IsNullOrEmpty(artist) ? title : $"{artist} — {title}";
		string playing = r.Track.Id == _playingTrackId ? "▶ " : "";
		return playing + r.Number + "  " + artistTitle;
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = bounds.Width;
		Driver.SetAttribute(ColorScheme.Normal);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(new string(' ', width));
		}

		if (_rows.Count == 0)
			return;

		for (int row = 0; row < bounds.Height; row++)
		{
			int idx = row + _scrollOffset;
			if (idx >= _rows.Count)
				break;

			bool isSelected = idx == _selectedIndex;
			bool isPlaying = _rows[idx].Track.Id == _playingTrackId;

			var attr = isSelected
				? ColorScheme.Focus
				: isPlaying ? ColorScheme.HotNormal : ColorScheme.Normal;
			Driver.SetAttribute(attr);

			string text = RowText(_rows[idx]);
			string render = isSelected && text.Length > width
				? MarqueeWindow(text, _marqueeOffset, width)
				: PadOrTruncate(text, width);

			Move(0, row);
			Driver.AddStr(render.Length > width ? render[..width] : render);
		}
	}

	private static string PadOrTruncate(string text, int width)
	{
		if (string.IsNullOrEmpty(text))
			return new string(' ', width);

		return text.Length >= width
			? text[..(width - 1)] + "…"
			: text.PadRight(width);
	}

	private static string MarqueeWindow(string text, int offset, int width)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= width)
			return (text ?? "").PadRight(width);

		int safe = Math.Clamp(offset, 0, text.Length - width);
		return text.Substring(safe, width);
	}

	public override bool ProcessKey(KeyEvent kb)
	{
		if (_rows.Count == 0)
			return base.ProcessKey(kb);

		int oldIndex = _selectedIndex;

		switch (kb.Key)
		{
			case Key.CursorUp:
				if (_selectedIndex > 0)
					_selectedIndex--;
				break;

			case Key.CursorDown:
				if (_selectedIndex < _rows.Count - 1)
					_selectedIndex++;
				break;

			case Key.PageUp:
				_selectedIndex = Math.Max(0, _selectedIndex - Bounds.Height);
				break;

			case Key.PageDown:
				_selectedIndex = Math.Min(_rows.Count - 1, _selectedIndex + Bounds.Height);
				break;

			case Key.Home:
				_selectedIndex = 0;
				break;

			case Key.End:
				_selectedIndex = _rows.Count - 1;
				break;

			case Key.Enter:
				OnCellActivated?.Invoke(_selectedIndex);
				return true;

			default:
				return base.ProcessKey(kb);
		}

		if (_selectedIndex != oldIndex)
		{
			ResetMarqueeState();
			EnsureSelectedVisible();
			OnTrackSelected?.Invoke(_selectedIndex);
			CheckNeedMoreTracks();
			SetNeedsDisplay();
		}

		return true;
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.WheeledDown))
		{
			int visible = Math.Max(1, Bounds.Height);
			if (_scrollOffset < Math.Max(0, _rows.Count - visible))
			{
				_scrollOffset++;
				CheckNeedMoreTracks();
				SetNeedsDisplay();
			}
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.WheeledUp))
		{
			if (_scrollOffset > 0)
			{
				_scrollOffset--;
				SetNeedsDisplay();
			}
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1Clicked))
		{
			if (!HasFocus)
				SetFocus();

			int index = me.Y + _scrollOffset;
			if (index >= 0 && index < _rows.Count)
			{
				int oldIndex = _selectedIndex;
				_selectedIndex = index;
				if (_selectedIndex != oldIndex)
				{
					ResetMarqueeState();
					OnTrackSelected?.Invoke(_selectedIndex);
					CheckNeedMoreTracks();
				}
				SetNeedsDisplay();
			}
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			int index = me.Y + _scrollOffset;
			if (index >= 0 && index < _rows.Count)
			{
				int oldIndex = _selectedIndex;
				_selectedIndex = index;
				if (_selectedIndex != oldIndex)
					ResetMarqueeState();
				OnCellActivated?.Invoke(_selectedIndex);
				SetNeedsDisplay();
			}
			return true;
		}

		return base.MouseEvent(me);
	}

	private void EnsureSelectedVisible()
	{
		int visible = Math.Max(1, Bounds.Height);
		if (_selectedIndex < _scrollOffset)
			_scrollOffset = _selectedIndex;
		else if (_selectedIndex >= _scrollOffset + visible)
			_scrollOffset = _selectedIndex - visible + 1;
	}

	private void CheckNeedMoreTracks()
	{
		if (_rows.Count == 0)
			return;

		int visible = Math.Max(1, Bounds.Height);
		if (!_isLoadingMore && _scrollOffset + visible >= _rows.Count - 10)
		{
			_isLoadingMore = true;
			NeedMoreTracks?.Invoke();
		}
	}

	// ── Бегущая строка (marquee) для выбранной строки ─────────────────

	private void StartMarquee()
	{
		StopMarquee();
		if (_rows.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _rows.Count)
			return;

		_marqueeOffset = 0;
		_marqueePause = 0;
		_marqueeToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(MarqueeIntervalMs), _ =>
		{
			int width = Math.Max(1, Bounds.Width);
			string text = RowText(_rows[_selectedIndex]);
			if (text.Length > width)
			{
				AdvanceMarquee(ref _marqueeOffset, ref _marqueePause, text.Length, width);
			}
			SetNeedsDisplay();
			return true;
		});
	}

	private void StopMarquee()
	{
		if (_marqueeToken != null)
		{
			Application.MainLoop.RemoveTimeout(_marqueeToken);
			_marqueeToken = null;
		}
	}

	private void ResetMarquee()
	{
		StopMarquee();
		StartMarquee();
	}

	private void ResetMarqueeState()
	{
		ResetMarquee();
	}

	private static void AdvanceMarquee(ref int offset, ref int pause, int length, int width)
	{
		int maxOffset = length - width;
		if (pause > 0)
		{
			pause--;
			if (pause == 0)
				offset = 0;
			return;
		}
		if (offset >= maxOffset)
		{
			pause = MarqueePauseTicks;
			return;
		}
		offset++;
	}
}
