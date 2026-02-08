using Autofac;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Services;
using YamBassPlayer.Views;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;

namespace YamBassPlayer.Presenters.Impl;

public class YandexSearchPresenter : IYandexSearchPresenter
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private List<Track> _searchResults = new();
	private bool _cancelled = true;

	public YandexSearchPresenter(YandexMusicApi api, AuthStorage storage, ITrackInfoProvider trackInfoProvider)
	{
		_api = api;
		_storage = storage;
		_trackInfoProvider = trackInfoProvider;
	}

	public void ShowYandexSearchDialog()
	{
		var view = ServicesProvider.Ioc.Resolve<IYandexSearchView>();

		_searchResults.Clear();
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
			var response = await _api.Search.TrackAsync(_storage, query, 0, 20);
			var tracks = response.Result.Tracks?.Results;

			if (tracks == null || tracks.Count == 0)
			{
				_searchResults.Clear();
				view.SetSearchResults(_searchResults);
				view.SetLoading(false);
				return;
			}

			var trackIds = tracks.Select(x => x.Id);
			var tracksInfo = await _trackInfoProvider.GetTracksInfoByIds(trackIds);
			_searchResults = tracksInfo.ToList();
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
		return _searchResults;
	}

	public bool WasCancelled()
	{
		return _cancelled;
	}
}
