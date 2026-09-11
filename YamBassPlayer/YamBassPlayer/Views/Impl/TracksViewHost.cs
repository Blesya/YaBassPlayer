using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Переключаемый контейнер для представления треков: хранит обе реализации
/// <see cref="ITracksView"/> (плитки и таблицу) и показывает одну из них.
/// Обе вьюхи рисуют одну общую <see cref="ITrackListModel"/>, поэтому список
/// не дублируется, а состояние меняется ровно один раз.
/// </summary>
public sealed class TracksViewHost : View, ITracksView
{
	private readonly TracksTileView _tiles;
	private readonly TracksView _table;
	private readonly ITrackListModel _model;
	private bool _useTiles;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksViewHost(TracksTileView tiles, TracksView table, ITrackListModel model)
	{
		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;

		_tiles = tiles;
		_table = table;
		_model = model;

		_tiles.OnTrackSelected += i => OnTrackSelected?.Invoke(i);
		_tiles.OnCellActivated += i => OnCellActivated?.Invoke(i);
		_tiles.NeedMoreTracks += () => NeedMoreTracks?.Invoke();
		_table.OnTrackSelected += i => OnTrackSelected?.Invoke(i);
		_table.OnCellActivated += i => OnCellActivated?.Invoke(i);
		_table.NeedMoreTracks += () => NeedMoreTracks?.Invoke();

		_useTiles = AppConfiguration.GetTracksViewMode();
		_tiles.Visible = _useTiles;
		_table.Visible = !_useTiles;

		Add(_tiles, _table);
	}

	public bool IsTilesActive => _useTiles;

	public void ToggleView()
	{
		_useTiles = !_useTiles;
		AppConfiguration.SaveTracksViewMode(_useTiles);
		ApplyActiveView();
	}

	public void SetTracksViewMode(bool useTiles)
	{
		if (_useTiles == useTiles)
			return;
		_useTiles = useTiles;
		ApplyActiveView();
	}

	private void ApplyActiveView()
	{
		_tiles.Visible = _useTiles;
		_table.Visible = !_useTiles;
		SetNeedsDisplay();

		if (HasFocus)
		{
			var active = (View)(_useTiles ? _tiles : _table);
			active.SetFocus();
		}
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
}
