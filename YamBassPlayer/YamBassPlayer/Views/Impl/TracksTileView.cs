using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class TracksTileView : View, ITracksView
{
	private const int TileWidth = 27;
	private const int TileHeight = 5;
	private const int TileGap = 1;

	private readonly record struct TileData(string DisplayNumber, string Artist, string Title, string Album);

	private readonly List<TileData> _tracks = [];
	private int _selectedIndex;
	private int _scrollOffset;
	private int _columns = 1;
	private bool _isLoadingMore;
	private int _revealedCount;
	private object? _animationToken;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksTileView()
	{
		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		Application.MainLoop.Invoke(() =>
		{
			StopRevealAnimation();
			_tracks.Clear();
			int number = 0;
			foreach (Track track in tracks)
			{
				number++;
				string displayNumber = isCached(track.Id) ? $"{number}*" : number.ToString();
				_tracks.Add(new TileData(displayNumber, track.Artist, track.Title, track.Album));
			}

			_selectedIndex = 0;
			_scrollOffset = 0;
			_isLoadingMore = false;
			StartRevealAnimation(0);
		});
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		Application.MainLoop.Invoke(() =>
		{
			int previousCount = _tracks.Count;
			int number = previousCount;
			foreach (Track track in tracks)
			{
				number++;
				string displayNumber = isCached(track.Id) ? $"{number}*" : number.ToString();
				_tracks.Add(new TileData(displayNumber, track.Artist, track.Title, track.Album));
			}

			_isLoadingMore = false;
			if (_animationToken == null)
				StartRevealAnimation(previousCount);
		});
	}

	public void ClearTracks()
	{
		Application.MainLoop.Invoke(() =>
		{
			StopRevealAnimation();
			_revealedCount = 0;
			_tracks.Clear();
			_selectedIndex = 0;
			_scrollOffset = 0;
			SetNeedsDisplay();
		});
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		_columns = Math.Max(1, bounds.Width / (TileWidth + TileGap));
		int visibleRows = Math.Max(1, bounds.Height / TileHeight);

		// Clear background
		Driver.SetAttribute(ColorScheme.Normal);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(new string(' ', bounds.Width));
		}

		if (_tracks.Count == 0)
			return;

		for (int row = 0; row < visibleRows; row++)
		{
			int gridRow = row + _scrollOffset;
			for (int col = 0; col < _columns; col++)
			{
				int index = gridRow * _columns + col;
				if (index >= _tracks.Count || index >= _revealedCount)
					break;

				int x = col * (TileWidth + TileGap);
				int y = row * TileHeight;

				bool isSelected = index == _selectedIndex;
				DrawTile(x, y, _tracks[index], isSelected, bounds);
			}
		}
	}

	private void DrawTile(int x, int y, TileData tile, bool isSelected, Rect bounds)
	{
		var attr = isSelected ? ColorScheme.Focus : ColorScheme.Normal;
		Driver.SetAttribute(attr);

		int innerWidth = TileWidth - 2;

		// Top border: ┌─── N ──────────────────────┐
		string numberPart = $" {tile.DisplayNumber} ";
		int dashesAfter = Math.Max(0, innerWidth - numberPart.Length);
		string topLine = "┌" + numberPart + new string('─', dashesAfter) + "┐";
		DrawStringAt(x, y, Truncate(topLine, TileWidth), bounds);

		// Artist line (highlighted)
		var artistAttr = isSelected ? ColorScheme.HotFocus : ColorScheme.HotNormal;
		Driver.SetAttribute(artistAttr);
		DrawStringAt(x, y + 1, "│" + PadOrTruncate(tile.Artist, innerWidth) + "│", bounds);
		Driver.SetAttribute(attr);

		// Title line
		DrawStringAt(x, y + 2, "│" + PadOrTruncate(tile.Title, innerWidth) + "│", bounds);

		// Album line
		DrawStringAt(x, y + 3, "│" + PadOrTruncate(tile.Album, innerWidth) + "│", bounds);

		// Bottom border
		DrawStringAt(x, y + 4, "└" + new string('─', innerWidth) + "┘", bounds);
	}

	private void DrawStringAt(int x, int y, string text, Rect bounds)
	{
		if (y < 0 || y >= bounds.Height)
			return;

		int maxLen = Math.Max(0, bounds.Width - x);
		if (maxLen <= 0)
			return;

		Move(x, y);
		Driver.AddStr(text.Length > maxLen ? text[..maxLen] : text);
	}

	private static string PadOrTruncate(string text, int width)
	{
		if (string.IsNullOrEmpty(text))
			return new string(' ', width);

		return text.Length >= width
			? text[..(width - 1)] + "…"
			: text.PadRight(width);
	}

	private static string Truncate(string text, int width)
	{
		return text.Length > width ? text[..width] : text;
	}

	public override bool ProcessKey(KeyEvent kb)
	{
		if (_tracks.Count == 0)
			return base.ProcessKey(kb);

		int oldIndex = _selectedIndex;

		switch (kb.Key)
		{
			case Key.CursorRight:
				if (_selectedIndex < _revealedCount - 1)
					_selectedIndex++;
				break;

			case Key.CursorLeft:
				if (_selectedIndex > 0)
					_selectedIndex--;
				break;

			case Key.CursorDown:
				if (_selectedIndex + _columns < _revealedCount)
					_selectedIndex += _columns;
				break;

			case Key.CursorUp:
				if (_selectedIndex - _columns >= 0)
					_selectedIndex -= _columns;
				break;

			case Key.Enter:
				OnCellActivated?.Invoke(_selectedIndex);
				return true;

			default:
				return base.ProcessKey(kb);
		}

		if (_selectedIndex != oldIndex)
		{
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
			int totalRows = (_tracks.Count + _columns - 1) / Math.Max(1, _columns);
			int visibleRows = Math.Max(1, Bounds.Height / TileHeight);
			if (_scrollOffset < totalRows - visibleRows)
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

			int col = me.X / (TileWidth + TileGap);
			int row = me.Y / TileHeight + _scrollOffset;
			int index = row * _columns + col;

			if (col < _columns && index >= 0 && index < _revealedCount)
			{
				int oldIndex = _selectedIndex;
				_selectedIndex = index;

				if (_selectedIndex != oldIndex)
				{
					OnTrackSelected?.Invoke(_selectedIndex);
					CheckNeedMoreTracks();
				}

				SetNeedsDisplay();
			}

			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			int col = me.X / (TileWidth + TileGap);
			int row = me.Y / TileHeight + _scrollOffset;
			int index = row * _columns + col;

			if (col < _columns && index >= 0 && index < _revealedCount)
			{
				_selectedIndex = index;
				OnCellActivated?.Invoke(_selectedIndex);
				SetNeedsDisplay();
			}

			return true;
		}

		return base.MouseEvent(me);
	}

	private void EnsureSelectedVisible()
	{
		if (_columns == 0)
			return;

		int selectedRow = _selectedIndex / _columns;
		int visibleRows = Math.Max(1, Bounds.Height / TileHeight);

		if (selectedRow < _scrollOffset)
			_scrollOffset = selectedRow;
		else if (selectedRow >= _scrollOffset + visibleRows)
			_scrollOffset = selectedRow - visibleRows + 1;
	}

	private void StartRevealAnimation(int fromIndex)
	{
		StopRevealAnimation();
		_revealedCount = fromIndex;

		if (_tracks.Count == 0)
			return;

		_animationToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			_revealedCount++;
			SetNeedsDisplay();

			if (_revealedCount >= _tracks.Count)
			{
				_animationToken = null;
				return false;
			}

			return true;
		});
	}

	private void StopRevealAnimation()
	{
		if (_animationToken != null)
		{
			Application.MainLoop.RemoveTimeout(_animationToken);
			_animationToken = null;
		}
	}

	private void CheckNeedMoreTracks()
	{
		if (_tracks.Count == 0 || _columns == 0)
			return;

		int totalRows = (_tracks.Count + _columns - 1) / _columns;
		int visibleRows = Math.Max(1, Bounds.Height / TileHeight);
		int thresholdRows = Math.Max(1, 30 / _columns);

		if (!_isLoadingMore && _scrollOffset + visibleRows >= totalRows - thresholdRows)
		{
			_isLoadingMore = true;
			NeedMoreTracks?.Invoke();
		}
	}
}
