using Autofac;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class LocalSearchPresenter : ILocalSearchPresenter
{
	private readonly ILocalLibraryService _localLibraryService;
	private List<Track> _searchResults = new();
	private List<Track> _selectedTracks = new();
	private bool _cancelled = true;

	public LocalSearchPresenter(ILocalLibraryService localLibraryService)
	{
		_localLibraryService = localLibraryService;
	}

	public void ShowLocalSearchDialog()
	{
		var view = ServicesProvider.Ioc.Resolve<ILocalSearchView>();

		_searchResults.Clear();
		_selectedTracks.Clear();
		_cancelled = true;

		view.OnSearchQueryChanged += (searchQuery) =>
		{
			PerformSearch(view, searchQuery);
		};

		view.OnOkClicked += () =>
		{
			if (_searchResults.Count == 0)
			{
				view.ShowError("Нет результатов для добавления в плейлист");
				return;
			}

			_selectedTracks = GetSelectedTracks(view);
			_cancelled = false;
			view.Close();
		};

		view.OnCancelClicked += () =>
		{
			_cancelled = true;
			view.Close();
		};

		view.Show();
	}

	private void PerformSearch(ILocalSearchView view, string searchQuery)
	{
		if (string.IsNullOrWhiteSpace(searchQuery))
		{
			_searchResults.Clear();
			_selectedTracks.Clear();
			view.SetSearchResults(_searchResults);
			return;
		}

		try
		{
			var results = _localLibraryService.SearchTracksAsync(searchQuery).GetAwaiter().GetResult();
			_searchResults = results.ToList();
			_selectedTracks.Clear();
			view.SetSearchResults(_searchResults);
		}
		catch (Exception ex)
		{
			view.ShowError($"Ошибка поиска: {ex.Message}");
		}
	}

	public List<Track> GetSelectedTracks()
	{
		return _selectedTracks;
	}

	private List<Track> GetSelectedTracks(ILocalSearchView view)
	{
		var markedTracks = view.GetMarkedTracks();
		return markedTracks.Count > 0 ? markedTracks.ToList() : _searchResults.ToList();
	}

	public bool WasCancelled()
	{
		return _cancelled;
	}
}
