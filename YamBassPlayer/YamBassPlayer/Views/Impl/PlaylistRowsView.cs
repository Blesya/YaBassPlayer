using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class PlaylistRowsView : View
{
	private const int CardHeight = 3;

	private readonly record struct CardData(string DisplayNumber, string Artist, string Title, string Album, string TrackId);

	private readonly List<CardData> _cards = [];
	private string? _currentTrackId;
	private int _selectedIndex;
	private int _scrollOffset;

	public Action<string>? OnTrackActivated;

	public PlaylistRowsView()
	{
		CanFocus = true;
	}

	public void SetTracks(IReadOnlyList<Track> tracks)
	{
		_cards.Clear();
		for (int i = 0; i < tracks.Count; i++)
		{
			Track t = tracks[i];
			_cards.Add(new CardData((i + 1).ToString(), t.Artist, t.Title, t.Album, t.Id));
		}

		_selectedIndex = 0;
		_scrollOffset = 0;
		ScrollToCurrentTrack();
		SetNeedsDisplay();
	}

	public void SetCurrentTrackId(string? trackId)
	{
		_currentTrackId = trackId;
		ScrollToCurrentTrack();
		SetNeedsDisplay();
	}

	private void ScrollToCurrentTrack()
	{
		if (_currentTrackId == null || _cards.Count == 0)
			return;

		int idx = _cards.FindIndex(c => c.TrackId == _currentTrackId);
		if (idx < 0)
			return;

		_selectedIndex = idx;
		EnsureSelectedVisible();
	}

	private void EnsureSelectedVisible()
	{
		int visibleRows = Math.Max(1, Bounds.Height / CardHeight);

		if (_selectedIndex < _scrollOffset)
			_scrollOffset = _selectedIndex;
		else if (_selectedIndex >= _scrollOffset + visibleRows)
			_scrollOffset = _selectedIndex - visibleRows + 1;
	}

	public override bool ProcessKey(KeyEvent kb)
	{
		if (_cards.Count == 0)
			return base.ProcessKey(kb);

		switch (kb.Key)
		{
			case Key.CursorUp:
				if (_selectedIndex > 0)
				{
					_selectedIndex--;
					EnsureSelectedVisible();
					SetNeedsDisplay();
				}
				return true;

			case Key.CursorDown:
				if (_selectedIndex < _cards.Count - 1)
				{
					_selectedIndex++;
					EnsureSelectedVisible();
					SetNeedsDisplay();
				}
				return true;

			case Key.Enter:
				OnTrackActivated?.Invoke(_cards[_selectedIndex].TrackId);
				return true;

			default:
				return base.ProcessKey(kb);
		}
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.WheeledDown))
		{
			int totalRows = _cards.Count;
			int visibleRows = Math.Max(1, Bounds.Height / CardHeight);
			if (_scrollOffset < totalRows - visibleRows)
			{
				_scrollOffset++;
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

		if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			if (!HasFocus)
				SetFocus();

			int row = me.Y / CardHeight + _scrollOffset;
			if (row >= 0 && row < _cards.Count)
			{
				_selectedIndex = row;
				EnsureSelectedVisible();
				SetNeedsDisplay();
				OnTrackActivated?.Invoke(_cards[_selectedIndex].TrackId);
			}
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1Clicked))
		{
			if (!HasFocus)
				SetFocus();

			int row = me.Y / CardHeight + _scrollOffset;
			if (row >= 0 && row < _cards.Count)
			{
				_selectedIndex = row;
				EnsureSelectedVisible();
				SetNeedsDisplay();
			}
			return true;
		}

		return base.MouseEvent(me);
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		Driver.SetAttribute(ColorScheme.Normal);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(new string(' ', bounds.Width));
		}

		if (_cards.Count == 0)
		{
			string msg = "Плейлист не загружен";
			Move(Math.Max(0, (bounds.Width - msg.Length) / 2), bounds.Height / 2);
			Driver.AddStr(msg);
			return;
		}

		int visibleRows = Math.Max(1, bounds.Height / CardHeight);

		for (int row = 0; row < visibleRows; row++)
		{
			int idx = row + _scrollOffset;
			if (idx >= _cards.Count)
				break;

			int y = row * CardHeight;
			DrawCard(0, y, _cards[idx], idx == _selectedIndex, _cards[idx].TrackId == _currentTrackId, bounds);
		}
	}

	private void DrawCard(int x, int y, CardData card, bool isSelected, bool isPlaying, Rect bounds)
	{
		var attr = isSelected ? ColorScheme.Focus : ColorScheme.Normal;
		Driver.SetAttribute(attr);

		int innerWidth = Math.Max(4, bounds.Width - 2);

		string numberPart = $" {card.DisplayNumber} ";
		string playingMark = isPlaying ? " ▶ " : "   ";
		int dashesAfter = Math.Max(0, innerWidth - numberPart.Length - playingMark.Length);
		string topLine = "┌" + numberPart + new string('─', dashesAfter) + playingMark + "┐";
		DrawAt(x, y, Truncate(topLine, bounds.Width), bounds);

		int contentWidth = Math.Max(1, innerWidth - 2);
		int artistWidth = Math.Max(1, contentWidth / 3);
		int titleWidth = Math.Max(1, contentWidth / 3);
		int albumWidth = Math.Max(1, contentWidth - artistWidth - titleWidth - 2);

		var artistAttr = isSelected ? ColorScheme.HotFocus : ColorScheme.HotNormal;
		Driver.SetAttribute(artistAttr);
		string artistStr = PadOrTruncate(card.Artist, artistWidth);
		Driver.SetAttribute(attr);
		string titleStr = PadOrTruncate(card.Title, titleWidth);
		string albumStr = PadOrTruncate(card.Album, albumWidth);

		string contentLine = "│ " + artistStr + "  " + titleStr + "  " + albumStr + " │";
		DrawAt(x, y + 1, Truncate(contentLine, bounds.Width), bounds);

		string bottomLine = "└" + new string('─', innerWidth) + "┘";
		DrawAt(x, y + 2, Truncate(bottomLine, bounds.Width), bounds);
	}

	private void DrawAt(int x, int y, string text, Rect bounds)
	{
		if (y < 0 || y >= bounds.Height)
			return;
		Move(x, y);
		Driver.AddStr(text);
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
		=> text.Length > width ? text[..width] : text;
}
