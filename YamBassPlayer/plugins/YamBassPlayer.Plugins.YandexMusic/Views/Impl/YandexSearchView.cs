using Terminal.Gui;
using YamBassPlayer.Models;

namespace YamBassPlayer.Views.Impl;

public class YandexSearchView : Dialog, IYandexSearchView
{
	private const string DefaultResultsLabelText = "Результаты: выполните поиск. Пробел отмечает элементы.";
	private readonly TextField _searchField;
	private readonly Button _searchButton;
	private readonly Label _resultsLabel;
	private readonly ListView _tracksListView;
	private readonly ListView _artistsListView;
	private readonly ListView _albumsListView;
	private List<Track> _tracks = new();
	private List<Artist> _artists = new();
	private List<Album> _albums = new();

	public event Action<string>? OnSearchClicked;
	public event Action? OnOkClicked;
	public event Action? OnCancelClicked;

	public YandexSearchView() : base("Поиск по ЯМ")
	{
		Width = 80;
		Height = 30;

		var searchLabel = new Label
		{
			Text = "Введите запрос для поиска:",
			X = 1,
			Y = 1,
			Width = Dim.Fill(1)
		};

		_searchField = new TextField
		{
			X = 1,
			Y = 2,
			Width = Dim.Fill(15)
		};

		_searchButton = new Button("Найти")
		{
			X = Pos.Right(_searchField) + 1,
			Y = 2
		};
		_searchButton.Clicked += () =>
		{
			string query = _searchField.Text?.ToString() ?? string.Empty;
			OnSearchClicked?.Invoke(query);
		};

		_resultsLabel = new Label
		{
			Text = DefaultResultsLabelText,
			X = 1,
			Y = 4,
			Width = Dim.Fill(1)
		};

		_tracksListView = CreateResultsList();
		_artistsListView = CreateResultsList();
		_albumsListView = CreateResultsList();

		var tabView = new TabView
		{
			X = 1,
			Y = 5,
			Width = Dim.Fill(1),
			Height = Dim.Fill(4)
		};
		tabView.AddTab(new TabView.Tab("Треки", _tracksListView), false);
		tabView.AddTab(new TabView.Tab("Исполнители", _artistsListView), false);
		tabView.AddTab(new TabView.Tab("Альбомы", _albumsListView), false);

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

		Add(searchLabel, _searchField, _searchButton, _resultsLabel, tabView, okButton, cancelButton);

		_searchField.SetFocus();
	}

	private static ListView CreateResultsList()
	{
		return new ListView
		{
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMarking = true,
			AllowsMultipleSelection = true
		};
	}

	public void SetSearchResults(SearchResult result)
	{
		_tracks = result.Tracks.ToList();
		_artists = result.Artists.ToList();
		_albums = result.Albums.ToList();

		_tracksListView.SetSource(_tracks
			.Select(t => $"{t.Artist} - {t.Title} ({t.Album})")
			.ToList());

		_artistsListView.SetSource(_artists
			.Select(a => a.Name)
			.ToList());

		_albumsListView.SetSource(_albums
			.Select(a => $"{a.Title} ({a.Year?.ToString() ?? "год неизвестен"})")
			.ToList());

		UpdateResultsLabel();
	}

	public IReadOnlyList<SearchResultItem> GetMarkedItems()
	{
		var markedItems = new List<SearchResultItem>();

		AddMarkedTracks(_tracksListView, _tracks, markedItems);
		AddMarkedArtists(_artistsListView, _artists, markedItems);
		AddMarkedAlbums(_albumsListView, _albums, markedItems);

		return markedItems;
	}

	public void SetLoading(bool isLoading)
	{
		_resultsLabel.Text = isLoading ? "Поиск..." : GetResultsLabelText();
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
		_resultsLabel.Text = GetResultsLabelText();
	}

	private string GetResultsLabelText()
	{
		int itemCount = _tracks.Count + _artists.Count + _albums.Count;
		return itemCount == 0
			? "Результаты: ничего не найдено."
			: $"Результаты: {itemCount}. Отметьте элементы пробелом и нажмите OK.";
	}

	private static void AddMarkedTracks(ListView listView, IReadOnlyList<Track> tracks, List<SearchResultItem> markedItems)
	{
		if (listView.Source is null)
			return;

		for (int i = 0; i < tracks.Count; i++)
		{
			if (listView.Source.IsMarked(i))
				markedItems.Add(new TrackItem(tracks[i]));
		}
	}

	private static void AddMarkedArtists(ListView listView, IReadOnlyList<Artist> artists, List<SearchResultItem> markedItems)
	{
		if (listView.Source is null)
			return;

		for (int i = 0; i < artists.Count; i++)
		{
			if (listView.Source.IsMarked(i))
				markedItems.Add(new ArtistItem(artists[i]));
		}
	}

	private static void AddMarkedAlbums(ListView listView, IReadOnlyList<Album> albums, List<SearchResultItem> markedItems)
	{
		if (listView.Source is null)
			return;

		for (int i = 0; i < albums.Count; i++)
		{
			if (listView.Source.IsMarked(i))
				markedItems.Add(new AlbumItem(albums[i]));
		}
	}
}
