using Autofac;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class LocalSearchPresenter : ILocalSearchPresenter
{
	private readonly ITrackInfoProvider _trackInfoProvider;
	private List<Track> _searchResults = new();
	private bool _cancelled = true;

	public LocalSearchPresenter(ITrackInfoProvider trackInfoProvider)
	{
		_trackInfoProvider = trackInfoProvider;
	}

	public void ShowLocalSearchDialog()
	{
		var view = ServicesProvider.Ioc.Resolve<ILocalSearchView>();
		
		_searchResults.Clear();
		_cancelled = true;

		view.OnSearchQueryChanged += async (searchQuery) =>
		{
			await PerformSearch(view, searchQuery);
		};

		view.OnOkClicked += () =>
		{
			if (_searchResults.Count == 0)
			{
				view.ShowError("Нет результатов для добавления в плейлист");
				return;
			}

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

	private async Task PerformSearch(ILocalSearchView view, string searchQuery)
	{
		if (string.IsNullOrWhiteSpace(searchQuery))
		{
			_searchResults.Clear();
			view.SetSearchResults(_searchResults);
			return;
		}

		try
		{
			var results = await _trackInfoProvider.SearchTracks(searchQuery, 50);
			_searchResults = results.ToList();
			view.SetSearchResults(_searchResults);
		}
		catch (Exception ex)
		{
			view.ShowError($"Ошибка поиска: {ex.Message}");
		}
	}

	public List<Track> GetSelectedTracks()
	{
		return _searchResults;
	}

	public bool WasCancelled()
	{
		return _cancelled;
	}
}
