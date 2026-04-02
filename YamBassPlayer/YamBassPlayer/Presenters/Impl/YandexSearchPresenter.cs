using Autofac;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Presenters.Impl;

public class YandexSearchPresenter : IYandexSearchPresenter
{
	private readonly ISourceSearchService _sourceSearchService;
	private List<Track> _searchResults = new();
	private List<Track> _selectedTracks = new();
	private bool _cancelled = true;

	public YandexSearchPresenter(ISourceSearchService sourceSearchService)
	{
		_sourceSearchService = sourceSearchService;
	}

	public void ShowYandexSearchDialog()
	{
		var view = ServicesProvider.Ioc.Resolve<IYandexSearchView>();

		_searchResults.Clear();
		_selectedTracks.Clear();
		_cancelled = true;

		view.OnSearchClicked += async (query) =>
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				view.ShowError("Введите текст для поиска");
				return;
			}

			await PerformSearchAsync(view, query);
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

	private async Task PerformSearchAsync(IYandexSearchView view, string query)
	{
		view.SetLoading(true);

		try
		{
			var tracks = await _sourceSearchService.SearchAsync("yandex", query, 20);
			_searchResults = tracks.ToList();
			_selectedTracks.Clear();
			view.SetSearchResults(_searchResults);
		}
		catch (Exception ex)
		{
			view.ShowError($"Ошибка поиска: {ex.Message}");
		}
		finally
		{
			view.SetLoading(false);
		}
	}

	public List<Track> GetSelectedTracks()
	{
		return _selectedTracks;
	}

	private List<Track> GetSelectedTracks(IYandexSearchView view)
	{
		var markedTracks = view.GetMarkedTracks();
		return markedTracks.Count > 0 ? markedTracks.ToList() : _searchResults.ToList();
	}

	public bool WasCancelled()
	{
		return _cancelled;
	}
}
