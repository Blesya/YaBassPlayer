using System.Text;
using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Список треков в одну колонку: маркер, номер, исполнитель, название и
/// длительность. Служебные поля приглушены, исполнитель выделен акцентом,
/// играющий трек помечен «▶», скачанный — «*» рядом с номером.
/// Выбранная строка подсвечивается только тогда, когда список действительно
/// в фокусе — иначе подсветка врала бы о том, где фокус.
/// Текст выбранной строки бежит строкой (marquee), если не помещается по ширине.
/// Состояние списка хранится в общей <see cref="ITrackListModel"/>.
/// </summary>
public sealed class TracksView : View, ITracksView
{
	private const int MarqueeIntervalMs = 250;
	private const int MarqueePauseTicks = 4;

	private const int MarkerWidth = 2;

	/// <summary>Ширина поля состояния кэша: « 45% », «   * », «     ».</summary>
	private const int StatusWidth = 5;

	private const int ScrollWidth = 1;
	private const int HeaderHeight = 1;
	private const int ColumnGap = 2;

	// Ниже этих ширин служебные колонки убираются, чтобы текст не сжимался в точку.
	private const int NumberColumnMinWidth = 30;
	private const int SplitColumnsMinWidth = 46;

	private const string PlayingMarker = "▶ ";

	private readonly ITrackListModel _model;

	private string _blankLine = "";
	private int _blankLineWidth = -1;

	private object? _marqueeToken;
	private int _marqueeOffset;
	private int _marqueePause;

	private int _lastItemCount = -1;
	private int _lastSelectedIndex = -1;

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

		// Подсветка выбранной строки зависит от фокуса, поэтому смена фокуса
		// должна перерисовывать список.
		Enter += _ => SetNeedsDisplay();
		Leave += _ => SetNeedsDisplay();
	}

	private int VisibleRows => Math.Max(1, Bounds.Height - HeaderHeight);

	/// <summary>
	/// Перерисовка после изменения модели. Бегущая строка перезапускается только когда
	/// сменился состав списка или выделение: обновление прогресса загрузки приходит
	/// десятки раз за трек и не должно дёргать анимацию.
	/// </summary>
	private void OnModelChanged()
	{
		if (_model.Items.Count != _lastItemCount || _model.SelectedIndex != _lastSelectedIndex)
		{
			_lastItemCount = _model.Items.Count;
			_lastSelectedIndex = _model.SelectedIndex;
			ResetMarquee();
		}

		SetNeedsDisplay();
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached)
		=> Application.MainLoop.Invoke(() => _model.SetTracks(tracks, isCached));

	public void AddTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached)
		=> Application.MainLoop.Invoke(() => _model.AddTracks(tracks, isCached));

	public void ClearTracks()
		=> Application.MainLoop.Invoke(_model.Clear);

	public void SetPlayingTrack(TrackKey? key)
		=> Application.MainLoop.Invoke(() => _model.SetPlayingTrack(key));

	public void SelectTrack(TrackKey key)
		=> Application.MainLoop.Invoke(() =>
		{
			if (_model.SelectByTrack(key))
				_model.EnsureSelectedVisible(VisibleRows);
		});

	public void SetFilter(string? filter)
		=> _model.SetFilter(filter);

	public void SetDownloadState(TrackKey key, TrackDownloadState state)
		=> Application.MainLoop.Invoke(() => _model.SetDownloadState(key, state));

	// ── Раскладка колонок ─────────────────────────────────────────────────

	/// <summary>
	/// Ширины полей строки. Нулевая ширина означает, что колонка не помещается
	/// в текущую ширину списка и не рисуется.
	/// </summary>
	private readonly record struct RowColumns(int Number, int Artist, int Title, int Duration, int Scroll)
	{
		public static RowColumns For(int width, int digits, int durationWidth, bool showScroll)
		{
			int number = width >= NumberColumnMinWidth ? digits + StatusWidth : 0;
			int scroll = showScroll ? ScrollWidth : 0;

			int flex = Math.Max(0, width - MarkerWidth - number - durationWidth - scroll);
			if (flex >= SplitColumnsMinWidth)
			{
				int artist = flex * 2 / 5;
				return new RowColumns(number, artist, flex - artist - ColumnGap, durationWidth, scroll);
			}

			return new RowColumns(number, flex, 0, durationWidth, scroll);
		}

		public int NamesWidth => Artist + (Title > 0 ? Title + ColumnGap : 0);
	}

	/// <summary>
	/// Ширина колонки времени: длина «m:ss» / «h:mm:ss» без форматирования строки.
	/// Раньше здесь на каждый элемент вызывался <c>ToShortDuration()</c>, то есть
	/// на каждую перерисовку списка создавались тысячи строк — даже когда нужна
	/// была одна цифра.
	/// </summary>
	private static int DurationWidth(IReadOnlyList<TrackListItem> items)
	{
		int max = 0;
		foreach (TrackListItem item in items)
		{
			int length = ShortDurationLength(item.Track.DurationMs);
			if (length > max)
				max = length;
		}

		// +1 — разделитель между названием и временем.
		return max == 0 ? 0 : max + 1;
	}

	/// <summary>Длина строки «m:ss» (для треков длиннее часа — «h:mm:ss»), без аллокаций.</summary>
	private static int ShortDurationLength(long? milliseconds)
	{
		if (milliseconds is not { } ms || ms <= 0)
			return 0;

		double minutes = ms / 60000.0;
		if (minutes >= 60)
			return minutes >= 600 ? 8 : 7; // hh:mm:ss / h:mm:ss

		return minutes >= 10 ? 5 : 4;      // mm:ss / m:ss
	}

	private static string ArtistTitle(TrackListItem item)
	{
		string artist = item.Track.Artist ?? "";
		string title = item.Track.Title ?? "";
		return string.IsNullOrEmpty(artist) ? title : $"{artist} — {title}";
	}

	private RowColumns ColumnsFor(int width, IReadOnlyList<TrackListItem> items)
		=> RowColumns.For(
			width,
			Math.Max(1, items.Count.ToString().Length),
			DurationWidth(items),
			items.Count > VisibleRows);

	// ── Отрисовка ─────────────────────────────────────────────────────────

	public override void Redraw(Rect bounds)
	{
		base.Redraw(bounds);

		int width = bounds.Width;
		Driver.SetAttribute(ColorScheme.Normal);
		string blankLine = GetBlankLine(width);
		for (int y = 0; y < bounds.Height; y++)
		{
			Move(0, y);
			Driver.AddStr(blankLine);
		}

		var items = _model.Items;
		if (items.Count == 0)
		{
			DrawEmptyState(bounds);
			return;
		}

		RowColumns columns = ColumnsFor(width, items);
		DrawHeader(columns);

		for (int row = 0; row < VisibleRows; row++)
		{
			int idx = row + _model.ScrollOffset;
			if (idx >= items.Count)
				break;

			DrawRow(row + HeaderHeight, idx, items[idx], columns);
		}

		if (columns.Scroll > 0)
			DrawScrollIndicator(items.Count);
	}

	private void DrawHeader(RowColumns c)
	{
		var line = new StringBuilder(new string(' ', MarkerWidth));

		if (c.Number > 0)
			line.Append("№".PadLeft(c.Number - StatusWidth)).Append(new string(' ', StatusWidth));

		line.Append(c.Title > 0
			? PadOrTruncate("Исполнитель", c.Artist) + new string(' ', ColumnGap) + PadOrTruncate("Название", c.Title)
			: PadOrTruncate("Исполнитель — название", c.Artist));

		if (c.Duration > 0)
			line.Append("Время".PadLeft(c.Duration));

		Driver.SetAttribute(Themes.DimFor(ColorScheme));
		Move(0, 0);
		Driver.AddStr(line.ToString());
	}

	private void DrawRow(int y, int index, TrackListItem item, RowColumns c)
	{
		bool isSelected = index == _model.SelectedIndex;
		bool isPlaying = item.Track.Key == _model.PlayingTrack;
		bool focused = isSelected && HasFocus;

		string marker = isPlaying ? PlayingMarker : new string(' ', MarkerWidth);

		Move(0, y);

		// Строка с фокусом подсвечивается целиком: цветные поля на фоне
		// подсветки читаются плохо, а так выделение однозначно.
		if (focused)
		{
			Driver.SetAttribute(ColorScheme.Focus);
			Driver.AddStr(marker);
			Driver.AddStr(RowBody(item, c));
			return;
		}

		Driver.SetAttribute(isPlaying ? ColorScheme.HotNormal : Themes.DimFor(ColorScheme));
		Driver.AddStr(marker);

		if (c.Number > 0)
		{
			Driver.SetAttribute(Themes.DimFor(ColorScheme));
			Driver.AddStr(NumberField(item, c));
		}

		if (c.Artist > 0)
		{
			if (c.Title > 0)
			{
				Driver.SetAttribute(ColorScheme.HotNormal);
				Driver.AddStr(PadOrTruncate(item.Track.Artist ?? "", c.Artist));
				Driver.SetAttribute(isPlaying ? ColorScheme.HotNormal : ColorScheme.Normal);
				Driver.AddStr(new string(' ', ColumnGap));
				Driver.AddStr(PadOrTruncate(item.Track.Title ?? "", c.Title));
			}
			else
			{
				Driver.SetAttribute(isPlaying ? ColorScheme.HotNormal : ColorScheme.Normal);
				Driver.AddStr(PadOrTruncate(ArtistTitle(item), c.Artist));
			}
		}

		if (c.Duration > 0)
		{
			Driver.SetAttribute(Themes.DimFor(ColorScheme));
			Driver.AddStr(item.Track.DurationMs.ToShortDuration().PadLeft(c.Duration));
		}
	}

	/// <summary>Номер трека и состояние кэша: «12», « 45%» пока качается, «*» когда скачан.</summary>
	private static string NumberField(TrackListItem item, RowColumns c)
		=> item.Number.PadLeft(c.Number - StatusWidth) + StatusField(item);

	/// <summary>Поле состояния кэша постоянной ширины, чтобы колонки не разъезжались.</summary>
	private static string StatusField(TrackListItem item)
	{
		TrackDownloadState download = item.Download;
		return download.Status switch
		{
			TrackDownloadStatus.Downloading => download.ToShortProgress().PadLeft(StatusWidth - 1) + " ",
			TrackDownloadStatus.Cached => "*".PadLeft(StatusWidth - 1) + " ",
			TrackDownloadStatus.Failed => "!".PadLeft(StatusWidth - 1) + " ",
			_ => new string(' ', StatusWidth)
		};
	}

	/// <summary>Тело строки одним цветом — для подсвеченной строки и для бегущей строки.</summary>
	private string RowBody(TrackListItem item, RowColumns c)
	{
		var body = new StringBuilder();

		if (c.Number > 0)
			body.Append(NumberField(item, c));

		if (c.Artist > 0)
			body.Append(NamesField(item, c, marquee: true));

		if (c.Duration > 0)
			body.Append(item.Track.DurationMs.ToShortDuration().PadLeft(c.Duration));

		return body.ToString();
	}

	/// <summary>
	/// Поля исполнителя и названия. Если имя целиком не помещается, оно бежит
	/// строкой; иначе каждое поле выравнивается по своей колонке.
	/// </summary>
	private string NamesField(TrackListItem item, RowColumns c, bool marquee)
	{
		string combined = ArtistTitle(item);
		if (c.Title <= 0)
			return PadOrTruncate(combined, c.Artist);

		if (marquee && combined.Length > c.NamesWidth)
			return MarqueeWindow(combined, _marqueeOffset, c.NamesWidth);

		return PadOrTruncate(item.Track.Artist ?? "", c.Artist)
			+ new string(' ', ColumnGap)
			+ PadOrTruncate(item.Track.Title ?? "", c.Title);
	}

	private void DrawEmptyState(Rect bounds)
	{
		const string message = "Выберите плейлист";
		Driver.SetAttribute(Themes.DimFor(ColorScheme));
		Move(Math.Max(0, (bounds.Width - message.Length) / 2), Math.Max(0, bounds.Height / 2));
		Driver.AddStr(message);
	}

	/// <summary>Тонкая полоса прокрутки в последней колонке списка.</summary>
	private void DrawScrollIndicator(int itemCount)
	{
		int trackHeight = VisibleRows;
		int thumbHeight = Math.Max(1, trackHeight * trackHeight / Math.Max(1, itemCount));
		int maxOffset = Math.Max(1, itemCount - trackHeight);
		int thumbTop = (int)Math.Round((double)_model.ScrollOffset / maxOffset * (trackHeight - thumbHeight));
		thumbTop = Math.Clamp(thumbTop, 0, Math.Max(0, trackHeight - thumbHeight));

		int x = Math.Max(0, Bounds.Width - ScrollWidth);
		for (int row = 0; row < trackHeight; row++)
		{
			bool isThumb = row >= thumbTop && row < thumbTop + thumbHeight;
			Driver.SetAttribute(isThumb ? ColorScheme.Normal : Themes.DimFor(ColorScheme));
			Move(x, row + HeaderHeight);
			Driver.AddStr(isThumb ? "█" : "│");
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

	// ── Ввод ──────────────────────────────────────────────────────────────

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

			int index = me.Y - HeaderHeight + _model.ScrollOffset;
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
			int index = me.Y - HeaderHeight + _model.ScrollOffset;
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

		// Бегущая строка нужна только если выделенная строка не помещается по ширине:
		// иначе таймер четыре раза в секунду дёргал бы полную перерисовку списка зря.
		if (!SelectedNeedsMarquee())
			return;

		_marqueeOffset = 0;
		_marqueePause = 0;
		_marqueeToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(MarqueeIntervalMs), _ =>
		{
			// Скрытая вьюха (включены плитки) анимацию не крутит.
			if (!Visible)
				return true;

			TrackListItem item = _model.Items[_model.SelectedIndex];
			int namesWidth = ColumnsFor(Math.Max(1, Bounds.Width), _model.Items).NamesWidth;
			string combined = ArtistTitle(item);

			if (namesWidth > 0 && combined.Length > namesWidth
				&& AdvanceMarquee(ref _marqueeOffset, ref _marqueePause, combined.Length, namesWidth))
			{
				SetNeedsDisplay();
			}

			return true;
		});
	}

	/// <summary>Помещается ли выделенная строка в колонку имён целиком.</summary>
	private bool SelectedNeedsMarquee()
	{
		TrackListItem item = _model.Items[_model.SelectedIndex];
		int namesWidth = ColumnsFor(Math.Max(1, Bounds.Width), _model.Items).NamesWidth;
		return namesWidth > 0 && ArtistTitle(item).Length > namesWidth;
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

	/// <summary>
	/// Сдвигает окно бегущей строки. Возвращает <c>true</c>, если картинка изменилась
	/// (в паузе между проходами и на краю сдвига перерисовывать нечего).
	/// </summary>
	private static bool AdvanceMarquee(ref int offset, ref int pause, int length, int width)
	{
		int maxOffset = length - width;
		if (pause > 0)
		{
			pause--;
			if (pause == 0 && offset != 0)
			{
				offset = 0;
				return true;
			}
			return false;
		}
		if (offset >= maxOffset)
		{
			pause = MarqueePauseTicks;
			return false;
		}
		offset++;
		return true;
	}
}
