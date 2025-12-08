using System.Data;
using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public sealed class TracksView : View, ITracksView
{
	private readonly ScrollableTableView _table;
	private readonly DataTable _dataTable;
	private bool _isLoadingMore;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksView()
	{
	    Width = Dim.Fill();
	    Height = Dim.Fill();

	    _dataTable = new DataTable();
	    _dataTable.Columns.Add("№", typeof(string));
	    _dataTable.Columns.Add("Исполнитель", typeof(string));
	    _dataTable.Columns.Add("Название", typeof(string));
	    _dataTable.Columns.Add("Альбом", typeof(string));

	    _table = new ScrollableTableView
	    {
	        Width = Dim.Fill(),
	        Height = Dim.Fill(),
	        Table = _dataTable,
	        FullRowSelect = true,
	    };

	    _table.OnScroll += CheckNeedMoreTracks;

	    _table.SelectedCellChanged += args =>
	    {
	        OnTrackSelected?.Invoke(args.NewRow);
	        CheckNeedMoreTracks();
	    };

	    _table.CellActivated += CellActivated;

	    Add(_table);
	}

	private void CellActivated(TableView.CellActivatedEventArgs cell)
	{
	    OnCellActivated?.Invoke(cell.Row);
	}

	private void CheckNeedMoreTracks()
	{
	    if (!_isLoadingMore && _table.RowOffset >= _dataTable.Rows.Count - 30)
	    {
	        _isLoadingMore = true;
	        _table.SetSelection(0, _dataTable.Rows.Count - 1, false);
	        NeedMoreTracks?.Invoke();
	    }
	}

	public void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        _dataTable.Rows.Clear();

	        foreach (Track track in tracks)
	        {
	            int number = _dataTable.Rows.Count + 1;
	            string displayNumber = isCached(track.Id) ? $"{number}*" : number.ToString();
	            _dataTable.Rows.Add(displayNumber, track.Artist, track.Title, track.Album);
	        }

	        _table.Update();
	        _isLoadingMore = false;
	    });
	}

	public void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached)
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        foreach (Track track in tracks)
	        {
	            int number = _dataTable.Rows.Count + 1;
	            string displayNumber = isCached(track.Id) ? $"{number}*" : number.ToString();
	            _dataTable.Rows.Add(displayNumber, track.Artist, track.Title, track.Album);
	        }

	        _table.Update();
	        _isLoadingMore = false;
	    });
	}

	public void ClearTracks()
	{
	    Application.MainLoop.Invoke(() =>
	    {
	        _dataTable.Rows.Clear();
	        _table.Update();
	    });
	}
}
