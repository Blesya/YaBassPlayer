using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Список треков плитками. Состояние списка хранится в общей
/// <see cref="ITrackListModel"/>; здесь остаются только раскладка, анимация
/// раскрытия плиток и бегущая строка.
/// </summary>
public sealed class TracksTileView : View, ITracksView
{
	private const int TileWidth = 26;
	private const int TileHeight = 5;
	private const int TileGap = 1;

	private const int MarqueeIntervalMs = 250;
	private const int MarqueePauseTicks = 4;
	private const int RevealBatchSize = 25;

	private readonly ITrackListModel _model;

	private int _columns = 1;
	private int _revealedCount;
	private int _syncedCount;
	private object? _animationToken;

	private string _blankLine = "";
	private int _blankLineWidth = -1;

	private int _marqueeArtistOffset;
	private int _marqueeTitleOffset;
	private int _marqueeAlbumOffset;
	private int _marqueePauseArtist;
	private int _marqueePauseTitle;
	private int _marqueePauseAlbum;
	private object? _marqueeToken;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksTileView() : this(new TrackListModel())
	{
	}

	public TracksTileView(ITrackListModel model)
	{
		_model = model;
		_model.Changed += OnModelChanged;

		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;
	}

	private int VisibleRows => Math.Max(1, Bounds.Height / TileHeight);
	private int VisibleItems => VisibleRows * Math.Max(1, _columns);
	private int ColumnStep => Math.Max(1, _columns);

	/// <summary>
	/// Реакция на изменение модели. Рост списка (пагинация) раскрывается
	/// анимацией с прежнего числа элементов; остальные изменения показываются сразу.
	/// </summary>
	private void OnModelChanged()
	{
		int count = _model.Items.Count;
		if (_syncedCount > 0 && count > _syncedCount)
		{
			if (_animationToken == null)
				StartRevealAnimation(_syncedCount);
		}
		else
		{
			StopRevealAnimation();
			_revealedCount = count;
		}

		_syncedCount = count;
		SetNeedsDisplay();
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		Application.MainLoop.Invoke(() =>
		{
			StopRevealAnimation();
			StopMarqueeTimer();
			_model.SetTracks(tracks, isCached);
			ResetMarqueeOffsets();
			if (_model.Items.Count > 0)
				StartMarqueeTimer();
			StartRevealAnimation(0);
		});
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
		=> Application.MainLoop.Invoke(() => _model.AddTracks(tracks, isCached));

	public void ClearTracks()
	{
		Application.MainLoop.Invoke(() =>
		{
			_model.Clear();
			StopRevealAnimation();
			StopMarqueeTimer();
			_revealedCount = 0;
			_syncedCount = 0;
			ResetMarqueeOffsets();
			SetNeedsDisplay();
		});
	}

	public void SetFilter(string? filter)
	{
		_model.SetFilter(filter);
		StopRevealAnimation();
		_revealedCount = _model.Items.Count;
		_syncedCount = _model.Items.Count;
		StopMarqueeTimer();
		ResetMarqueeOffsets();
		if (_model.Items.Count > 0)
			StartMarqueeTimer();
		SetNeedsDisplay();
	}

	public void SetPlayingTrackId(string? trackId)
		=> Application.MainLoop.Invoke(() => _model.SetPlayingTrackId(trackId));

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		_columns = Math.Max(1, bounds.Width / (TileWidth + TileGap));
		int visibleRows = Math.Max(1, bounds.Height / TileHeight);

		// Clear background
		string blankLine = GetBlankLine(bounds.Width);
		Driver.SetAttribute(ColorScheme.Normal);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(blankLine);
		}

		var items = _model.Items;
		if (items.Count == 0)
			return;

		for (int row = 0; row < visibleRows; row++)
		{
			for (int col = 0; col < _columns; col++)
			{
				int index = _model.ScrollOffset + row * _columns + col;
				if (index >= items.Count || index >= _revealedCount)
					break;

				int x = col * (TileWidth + TileGap);
				int y = row * TileHeight;

				bool isSelected = index == _model.SelectedIndex;
				bool isPlaying = items[index].Track.Id == _model.PlayingTrackId;
				DrawTile(x, y, items[index], isSelected, isPlaying, bounds);
			}
		}
	}

	private void DrawTile(int x, int y, TrackListItem item, bool isSelected, bool isPlaying, Rect bounds)
	{
		var attr = isSelected ? ColorScheme.Focus : ColorScheme.Normal;
		Driver.SetAttribute(attr);

		int innerWidth = TileWidth - 2;

		// Top border: ┌─── N ──────────────────────┐  or  ┌─── N ────────────────── ▶┐
		string numberPart = $" {item.Number.Trim()} ";
		string playingMark = isPlaying ? " ▶ " : "";
		int dashesAfter = Math.Max(0, innerWidth - numberPart.Length - playingMark.Length);
		string topLine = "┌" + numberPart + new string('─', dashesAfter) + playingMark + "┐";
		DrawStringAt(x, y, Truncate(topLine, TileWidth), bounds);

		// Artist line (highlighted)
		var artistAttr = isSelected ? ColorScheme.HotFocus : ColorScheme.HotNormal;
		Driver.SetAttribute(artistAttr);
		string artistText = isSelected
			? MarqueeText(item.Track.Artist, _marqueeArtistOffset, innerWidth)
			: PadOrTruncate(item.Track.Artist, innerWidth);
		DrawStringAt(x, y + 1, "│" + artistText + "│", bounds);
		Driver.SetAttribute(attr);

		// Title line
		string titleText = isSelected
			? MarqueeText(item.Track.Title, _marqueeTitleOffset, innerWidth)
			: PadOrTruncate(item.Track.Title, innerWidth);
		DrawStringAt(x, y + 2, "│" + titleText + "│", bounds);

		// Album / Subtitle line
		string thirdLine = item.Track.Subtitle ?? item.Track.Album;
		string albumText = isSelected
			? MarqueeText(thirdLine, _marqueeAlbumOffset, innerWidth)
			: PadOrTruncate(thirdLine, innerWidth);
		DrawStringAt(x, y + 3, "│" + albumText + "│", bounds);

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

	private string GetBlankLine(int width)
	{
		if (_blankLineWidth != width)
		{
			_blankLine = new string(' ', width);
			_blankLineWidth = width;
		}

		return _blankLine;
	}

	private static string PadOrTruncate(string text, int width)
	{
		if (string.IsNullOrEmpty(text))
			return new string(' ', width);

		return text.Length >= width
			? text[..(width - 1)] + "…"
			: text.PadRight(width);
	}

	private static string MarqueeText(string text, int offset, int width)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= width)
			return (text ?? "").PadRight(width);

		int safeOffset = Math.Clamp(offset, 0, text.Length - width);
		return text.Substring(safeOffset, width);
	}

	private static string Truncate(string text, int width)
	{
		return text.Length > width ? text[..width] : text;
	}

	public override bool ProcessKey(KeyEvent kb)
	{
		if (_model.Items.Count == 0)
			return base.ProcessKey(kb);

		bool moved;
		switch (kb.Key)
		{
			case Key.CursorRight:
				moved = _model.MoveDown(1);
				break;

			case Key.CursorLeft:
				moved = _model.MoveUp(1);
				break;

			case Key.CursorDown:
				moved = _model.MoveDown(ColumnStep);
				break;

			case Key.CursorUp:
				moved = _model.MoveUp(ColumnStep);
				break;

			case Key.Enter:
				OnCellActivated?.Invoke(_model.SelectedIndex);
				return true;

			default:
				return base.ProcessKey(kb);
		}

		if (moved)
		{
			_model.EnsureSelectedVisible(VisibleItems, ColumnStep);
			OnTrackSelected?.Invoke(_model.SelectedIndex);
			if (_model.ShouldRequestMore(VisibleItems))
				NeedMoreTracks?.Invoke();
		}

		return true;
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.WheeledDown))
		{
			if (_model.ScrollBy(ColumnStep, VisibleItems) && _model.ShouldRequestMore(VisibleItems))
				NeedMoreTracks?.Invoke();
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.WheeledUp))
		{
			_model.ScrollBy(-ColumnStep, VisibleItems);
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1Clicked))
		{
			if (!HasFocus)
				SetFocus();

			if (TryGetTileIndex(me, out int index) && _model.Select(index))
			{
				OnTrackSelected?.Invoke(_model.SelectedIndex);
				if (_model.ShouldRequestMore(VisibleItems))
					NeedMoreTracks?.Invoke();
			}

			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			if (TryGetTileIndex(me, out int index))
			{
				_model.Select(index);
				OnCellActivated?.Invoke(_model.SelectedIndex);
			}

			return true;
		}

		return base.MouseEvent(me);
	}

	private bool TryGetTileIndex(MouseEvent me, out int index)
	{
		int col = me.X / (TileWidth + TileGap);
		int row = me.Y / TileHeight;
		index = _model.ScrollOffset + row * ColumnStep + col;
		return col < ColumnStep && index >= 0 && index < _model.Items.Count && index < _revealedCount;
	}

	private void StartRevealAnimation(int fromIndex)
	{
		StopRevealAnimation();
		_revealedCount = Math.Clamp(fromIndex, 0, _model.Items.Count);

		if (_model.Items.Count == 0)
			return;

		_animationToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(16), _ =>
		{
			_revealedCount = Math.Min(_revealedCount + RevealBatchSize, _model.Items.Count);
			SetNeedsDisplay();

			if (_revealedCount >= _model.Items.Count)
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

	private void StartMarqueeTimer()
	{
		if (_marqueeToken != null)
			return;

		_marqueeToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(MarqueeIntervalMs), _ =>
		{
			// Скрытая вью не должна крутить бегущую строку: таймер простаивает без перерисовки.
			if (!Visible)
				return true;

			if (_model.Items.Count == 0 || _model.SelectedIndex < 0 || _model.SelectedIndex >= _model.Items.Count)
				return true;

			int innerWidth = TileWidth - 2;
			TrackListItem item = _model.Items[_model.SelectedIndex];

			AdvanceMarquee(item.Track.Artist, innerWidth, ref _marqueeArtistOffset, ref _marqueePauseArtist);
			AdvanceMarquee(item.Track.Title, innerWidth, ref _marqueeTitleOffset, ref _marqueePauseTitle);
			AdvanceMarquee(item.Track.Subtitle ?? item.Track.Album, innerWidth, ref _marqueeAlbumOffset, ref _marqueePauseAlbum);

			SetNeedsDisplay();
			return true;
		});
	}

	private static void AdvanceMarquee(string text, int width, ref int offset, ref int pause)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= width)
			return;

		int maxOffset = text.Length - width;

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

	private void StopMarqueeTimer()
	{
		if (_marqueeToken != null)
		{
			Application.MainLoop.RemoveTimeout(_marqueeToken);
			_marqueeToken = null;
		}
	}

	private void ResetMarqueeOffsets()
	{
		_marqueeArtistOffset = 0;
		_marqueeTitleOffset = 0;
		_marqueeAlbumOffset = 0;
		_marqueePauseArtist = 0;
		_marqueePauseTitle = 0;
		_marqueePauseAlbum = 0;
	}
}
