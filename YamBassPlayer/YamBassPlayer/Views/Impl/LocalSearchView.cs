using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public class LocalSearchView : Dialog, ILocalSearchView
{
	private const string EmptyResultsLabelText = "Результаты: введите запрос. Пробел отмечает треки.";
	private readonly TextField _searchField;
	private readonly ListView _resultsListView;
	private readonly Label _resultsLabel;
	private List<Track> _searchResults = new();

	public event Action? OnOkClicked;
	public event Action? OnCancelClicked;
	public event Action<string>? OnSearchQueryChanged;

	public LocalSearchView() : base("Локальный поиск")
	{
		Width = 80;
		Height = 30;

		var searchLabel = new Label
		{
			Text = "Поиск треков (по названию, исполнителю, альбому):",
			X = 1,
			Y = 1,
			Width = Dim.Fill(1)
		};

		_searchField = new TextField
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill(1)
		};
		_searchField.TextChanged += _ => 
		{
			string query = _searchField.Text?.ToString() ?? string.Empty;
			OnSearchQueryChanged?.Invoke(query);
		};

		_resultsLabel = new Label
		{
			Text = EmptyResultsLabelText,
			X = 1,
			Y = 4,
			Width = Dim.Fill(1)
		};

		_resultsListView = new ListView
		{
			X = 1,
			Y = 5,
			Width = Dim.Fill(1),
			Height = Dim.Fill(4),
			AllowsMarking = true,
			AllowsMultipleSelection = true
		};

		var okButton = new Button("OK")
		{
			X = Pos.Center() - 10,
			Y = Pos.AnchorEnd(2)
		};
		okButton.Clicked += () => OnOkClicked?.Invoke();

		var cancelButton = new Button("Отмена")
		{
			X = Pos.Center() + 2,
			Y = Pos.AnchorEnd(2)
		};
		cancelButton.Clicked += () => OnCancelClicked?.Invoke();

		Add(searchLabel, _searchField, _resultsLabel, _resultsListView, okButton, cancelButton);

		_searchField.SetFocus();
	}

	public void SetSearchResults(IEnumerable<Track> tracks)
	{
		_searchResults = tracks.ToList();
		
		var displayList = _searchResults
			.Select(t => $"{t.Artist} - {t.Title} ({t.Album})")
			.ToList();
		
		_resultsListView.SetSource(displayList);
		UpdateResultsLabel();
	}

	public IReadOnlyList<Track> GetMarkedTracks()
	{
		if (_resultsListView.Source is null)
		{
			return [];
		}

		var markedTracks = new List<Track>();
		for (int i = 0; i < _searchResults.Count; i++)
		{
			if (_resultsListView.Source.IsMarked(i))
			{
				markedTracks.Add(_searchResults[i]);
			}
		}

		return markedTracks;
	}

	public void Show()
	{
		Application.Run(this);
	}

	public void Close()
	{
		Application.RequestStop();
	}

	public void ShowError(string message)
	{
		MessageBox.ErrorQuery("Ошибка", message, "OK");
	}

	private void UpdateResultsLabel()
	{
		_resultsLabel.Text = _searchResults.Count == 0
			? "Результаты: ничего не найдено."
			: $"Результаты: {_searchResults.Count}. Пробел отмечает треки, OK добавляет отмеченные.";
	}
}
