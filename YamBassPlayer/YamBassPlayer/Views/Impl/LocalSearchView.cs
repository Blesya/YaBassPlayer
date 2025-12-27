using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public class LocalSearchView : Dialog, ILocalSearchView
{
    private readonly TextField _searchField;
    private readonly ListView _resultsListView;
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

        var resultsLabel = new Label
        {
            Text = "Результаты (макс. 50):",
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
            AllowsMarking = false
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

        Add(searchLabel, _searchField, resultsLabel, _resultsListView, okButton, cancelButton);

        _searchField.SetFocus();
    }

    public void SetSearchResults(IEnumerable<Track> tracks)
    {
        _searchResults = tracks.ToList();
        
        var displayList = _searchResults
            .Select(t => $"{t.Artist} - {t.Title} ({t.Album})")
            .ToList();
        
        _resultsListView.SetSource(displayList);
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
}
