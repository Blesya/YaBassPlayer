using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Список треков в одну колонку. Каждая строка: «№  Исполнитель — Название»,
/// справа выровнена длительность трека.
/// Текст выбранной строки бежит строкой (marquee), если не помещается по ширине.
/// Состояние списка хранится в общей <see cref="ITrackListModel"/>.
/// </summary>
public sealed class TracksView : View, ITracksView
{
	private const int MarqueeIntervalMs = 250;
	private const int MarqueePauseTicks = 4;

	private readonly ITrackListModel _model;

	private string _blankLine = "";
	private int _blankLineWidth = -1;

	private object? _marqueeToken;
	private int _marqueeOffset;
	private int _marqueePause;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksView() : this(new TrackListModel())
	{
	}

	public TracksView(ITrackListModel model)
	{
		_model = model;
		_model.Changed += OnModelChanged;

		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;
	}

	private int VisibleRows => Math.Max(1, Bounds.Height);

	private void OnModelChanged()
	{
		ResetMarquee();
		SetNeedsDisplay();
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
		=> Application.MainLoop.Invoke(() => _model.SetTracks(tracks, isCached));

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
		=> Application.MainLoop.Invoke(() => _model.AddTracks(tracks, isCached));

	public void ClearTracks()
		=> Application.MainLoop.Invoke(_model.Clear);

	public void SetPlayingTrackId(string? trackId)
		=> Application.MainLoop.Invoke(() => _model.SetPlayingTrackId(trackId));

	public void SetFilter(string? filter)
		=> _model.SetFilter(filter);

	private string RowText(TrackListItem item)
	{
		string artist = item.Track.Artist ?? "";
		string title = item.Track.Title ?? "";
		string artistTitle = string.IsNullOrEmpty(artist) ? title : $"{artist} — {title}";
		string playing = item.Track.Id == _model.PlayingTrackId ? "▶ " : "";
		return playing + item.Number + "  " + artistTitle;
	}

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = bounds.Width;
		string blankLine = GetBlankLine(width);
		Driver.SetAttribute(ColorScheme.Normal);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(blankLine);
		}

		var items = _model.Items;
		if (items.Count == 0)
			return;

		for (int row = 0; row < bounds.Height; row++)
		{
			int idx = row + _model.ScrollOffset;
			if (idx >= items.Count)
				break;

			bool isSelected = idx == _model.SelectedIndex;
			bool isPlaying = items[idx].Track.Id == _model.PlayingTrackId;

			var attr = isSelected
				? ColorScheme.Focus
				: isPlaying ? ColorScheme.HotNormal : ColorScheme.Normal;
			Driver.SetAttribute(attr);

			            string text = RowText(items[idx]);
			string duration = items[idx].Track.DurationMs.ToShortDuration();
			int reserve = duration.Length == 0 ? 0 : duration.Length + 1;
			int contentWidth = Math.Max(0, width - reserve);

			string content = isSelected && text.Length > contentWidth
				? MarqueeWindow(text, _marqueeOffset, contentWidth)
				: PadOrTruncate(text, contentWidth);

			string render = reserve == 0 ? content : content + " " + duration;

			Move(0, row);
			Driver.AddStr(render.Length > width ? render[..width] : render);
		}
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
	        if (width <= 0)
	            return string.Empty;

	        if (string.IsNullOrEmpty(text))
	            return new string(' ', width);

	        return text.Length >= width
	            ? text[..(width - 1)] + "…"
	            : text.PadRight(width);
	    }

	    private static string MarqueeWindow(string text, int offset, int width)
	    {
	        if (width <= 0)
	            return string.Empty;

	        if (string.IsNullOrEmpty(text) || text.Length <= width)
	            return (text ?? "").PadRight(width);

	        int safe = Math.Clamp(offset, 0, text.Length - width);
	        return text.Substring(safe, width);
	    }

	public override bool ProcessKey(KeyEvent kb)
	{
		if (_model.Items.Count == 0)
			return base.ProcessKey(kb);

		bool moved;
		switch (kb.Key)
		{
			case Key.CursorUp:
				moved = _model.MoveUp(1);
				break;

			case Key.CursorDown:
				moved = _model.MoveDown(1);
				break;

			case Key.PageUp:
				moved = _model.PageUp(VisibleRows);
				break;

			case Key.PageDown:
				moved = _model.PageDown(VisibleRows);
				break;

			case Key.Home:
				moved = _model.Home();
				break;

			case Key.End:
				moved = _model.End();
				break;

			case Key.Enter:
				OnCellActivated?.Invoke(_model.SelectedIndex);
				return true;

			default:
				return base.ProcessKey(kb);
		}

		if (moved)
		{
			_model.EnsureSelectedVisible(VisibleRows);
			OnTrackSelected?.Invoke(_model.SelectedIndex);
			if (_model.ShouldRequestMore(VisibleRows))
				NeedMoreTracks?.Invoke();
		}

		return true;
	}

	public override bool MouseEvent(MouseEvent me)
	{
		if (me.Flags.HasFlag(MouseFlags.WheeledDown))
		{
			if (_model.ScrollBy(1, VisibleRows) && _model.ShouldRequestMore(VisibleRows))
				NeedMoreTracks?.Invoke();
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.WheeledUp))
		{
			_model.ScrollBy(-1, VisibleRows);
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1Clicked))
		{
			if (!HasFocus)
				SetFocus();

			int index = me.Y + _model.ScrollOffset;
			if (index >= 0 && index < _model.Items.Count && _model.Select(index))
			{
				OnTrackSelected?.Invoke(_model.SelectedIndex);
				if (_model.ShouldRequestMore(VisibleRows))
					NeedMoreTracks?.Invoke();
			}
			return true;
		}

		if (me.Flags.HasFlag(MouseFlags.Button1DoubleClicked))
		{
			int index = me.Y + _model.ScrollOffset;
			if (index >= 0 && index < _model.Items.Count)
			{
				_model.Select(index);
				OnCellActivated?.Invoke(_model.SelectedIndex);
			}
			return true;
		}

		return base.MouseEvent(me);
	}

	// ── Бегущая строка (marquee) для выбранной строки ─────────────────

	private void StartMarquee()
	{
		StopMarquee();
		if (_model.Items.Count == 0 || _model.SelectedIndex < 0 || _model.SelectedIndex >= _model.Items.Count)
			return;

		_marqueeOffset = 0;
		_marqueePause = 0;
		_marqueeToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(MarqueeIntervalMs), _ =>
		{
			int width = Math.Max(1, Bounds.Width);
			TrackListItem item = _model.Items[_model.SelectedIndex];
			string text = RowText(item);

			string duration = item.Track.DurationMs.ToShortDuration();
			int contentWidth = Math.Max(1, width - (duration.Length == 0 ? 0 : duration.Length + 1));

			if (text.Length > contentWidth)
			{
				AdvanceMarquee(ref _marqueeOffset, ref _marqueePause, text.Length, contentWidth);
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
