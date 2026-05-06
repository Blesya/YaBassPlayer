using Terminal.Gui;
using YamBassPlayer.Configuration;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Переключаемый контейнер для представления треков: хранит обе реализации
/// <see cref="ITracksView"/> (плитки и таблицу) и показывает одну из них.
/// Все методы состояния отправляются в обе, поэтому переключение мгновенно
/// показывает текущий (уже загруженный) список без перезапуска плеера.
/// </summary>
public sealed class TracksViewHost : View, ITracksView
{
	private readonly TracksTileView _tiles;
	private readonly TracksView _table;
	private bool _useTiles;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksViewHost(TracksTileView tiles, TracksView table)
	{
		Width = Dim.Fill();
		Height = Dim.Fill();
		CanFocus = true;

		_tiles = tiles;
		_table = table;

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
	{
		_tiles.SetTracks(tracks, isCached);
		_table.SetTracks(tracks, isCached);
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
		_tiles.AddTracks(tracks, isCached);
		_table.AddTracks(tracks, isCached);
	}

	public void ClearTracks()
	{
		_tiles.ClearTracks();
		_table.ClearTracks();
	}

	public void SetPlayingTrackId(string? trackId)
	{
		_tiles.SetPlayingTrackId(trackId);
		_table.SetPlayingTrackId(trackId);
	}

	public void SetFilter(string? filter)
	{
		_tiles.SetFilter(filter);
		_table.SetFilter(filter);
	}
}
