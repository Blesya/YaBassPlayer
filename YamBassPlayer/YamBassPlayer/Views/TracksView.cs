using System.Data;
using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

public sealed class TracksView : View
{
	private readonly TableView _table;
	private readonly DataTable _dataTable;

	public event Action<int>? OnTrackSelected;
	public event Action<int>? OnCellActivated;
	public event Action? NeedMoreTracks;

	public TracksView()
	{
		Width = Dim.Fill();
		Height = Dim.Fill();

		_dataTable = new DataTable();
		_dataTable.Columns.Add("№", typeof(int));
		_dataTable.Columns.Add("Исполнитель", typeof(string));
		_dataTable.Columns.Add("Название", typeof(string));
		_dataTable.Columns.Add("Альбом", typeof(string));

		_table = new TableView
		{
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Table = _dataTable,
			FullRowSelect = true
		};

		_table.SelectedCellChanged += args =>
		{
			OnTrackSelected?.Invoke(args.NewRow);
		};

		_table.CellActivated += CellActivated;

		_table.KeyPress += args =>
		{
			if (args.KeyEvent.Key == Key.CursorDown &&
			    _table.SelectedRow >= _dataTable.Rows.Count - 2)
			{
				NeedMoreTracks?.Invoke();
			}
		};

		Add(_table);
	}

	private void CellActivated(TableView.CellActivatedEventArgs cell)
	{
		OnCellActivated?.Invoke(cell.Row);
	}

	public void SetTracks(IEnumerable<Track> tracks)
	{
		Application.MainLoop.Invoke(() =>
		{
			_dataTable.Rows.Clear();

			foreach (Track track in tracks)
			{
				_dataTable.Rows.Add(_dataTable.Rows.Count + 1, track.Artist, track.Title, track.Album);
			}

			_table.Update();
		});
	}

	public void AddTracks(IEnumerable<Track> tracks)
	{
		Application.MainLoop.Invoke(() =>
		{
			foreach (Track track in tracks)
			{
				_dataTable.Rows.Add(_dataTable.Rows.Count + 1, track.Artist, track.Title, track.Album);
			}

			_table.Update();
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