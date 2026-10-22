using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Presenters.Impl;

public class YandexSearchPresenter : IYandexSearchPresenter
{
	private readonly IEntitySearchSource _source;
	private readonly Func<IYandexSearchView> _createView;
	
	private List<Track> _selectedTracks = new();
	private bool _cancelled = true;

	public YandexSearchPresenter(IEntitySearchSource source, Func<IYandexSearchView> createView)
	{
		_source = source;
		_createView = createView;
	}

	public void ShowYandexSearchDialog()
	{
		var view = _createView();

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

		view.OnOkClicked += async () =>
		{
			try
			{
				var markedItems = view.GetMarkedItems();
				if (markedItems.Count == 0)
				{
					view.ShowError("Нет результатов для добавления в плейлист");
					return;
				}

				_selectedTracks = await ExpandItemsAsync(markedItems);
				_cancelled = false;
				view.Close();
			}
			catch (Exception ex)
			{
				view.ShowError($"Ошибка поиска: {ex.Message}");
			}
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
			var result = await _source.SearchAllAsync(query, 20);
			_selectedTracks.Clear();
			view.SetSearchResults(result);
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

	private async Task<List<Track>> ExpandItemsAsync(IReadOnlyList<SearchResultItem> items)
	{
		var tracks = new List<Track>();
		foreach (var item in items)
		{
			switch (item)
			{
				case TrackItem trackItem:
					tracks.Add(trackItem.Track);
					break;
				case ArtistItem artistItem:
					tracks.AddRange(await _source.GetArtistTracksAsync(artistItem.Artist.Id));
					break;
				case AlbumItem albumItem:
					tracks.AddRange(await _source.GetAlbumTracksAsync(albumItem.Album.Id));
					break;
			}
		}

		return tracks;
	}

	public List<Track> GetSelectedTracks()
	{
		return _selectedTracks;
	}

	public bool WasCancelled()
	{
		return _cancelled;
	}
}
