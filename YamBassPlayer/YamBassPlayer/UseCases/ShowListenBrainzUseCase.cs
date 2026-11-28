using Serilog;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Impl;

namespace YamBassPlayer.UseCases;

/// <summary>
/// Показ рекомендаций ListenBrainz. Сервис отдаёт уже сопоставленные треки,
/// сценарий складывает их в плейлист ядра и запускает воспроизведение — так же,
/// как это делает «Моя волна», только без источника за спиной.
/// </summary>
public sealed class ShowListenBrainzUseCase(
	IListenBrainzService listenBrainz,
	ListenBrainzLoadStrategy loadStrategy,
	ITrackInfoProvider trackInfoProvider,
	IPlaylistsPresenter playlistsPresenter,
	ITracksPresenter tracksPresenter,
	IPlayStatusPresenter playStatusPresenter,
	IPlaybackQueue playbackQueue)
{
	private const string RecommendationsTitle = "ListenBrainz: рекомендации";
	private const string RadioTitle = "ListenBrainz: радио";

	public Task ShowRecommendationsAsync(Action<string> setWindowTitle)
	{
		if (!listenBrainz.IsConfigured)
		{
			playStatusPresenter.SetPlayStatus(
				"ListenBrainz: не настроен — нужны ListenBrainz:User и ListenBrainz:Token");
			return Task.CompletedTask;
		}

		return ShowAsync(
			RecommendationsTitle,
			"Подобрано по истории прослушиваний",
			ct => listenBrainz.GetRecommendationsAsync(ct: ct),
			setWindowTitle);
	}

	/// <summary>
	/// LB Radio по жанру текущего трека: работает без истории, но требует тег —
	/// по имени артиста сервис треки не ищет.
	/// </summary>
	public async Task ShowRadioAsync(Action<string> setWindowTitle)
	{
		if (!listenBrainz.HasToken)
		{
			playStatusPresenter.SetPlayStatus("ListenBrainz: не настроен — нужен ListenBrainz:Token");
			return;
		}

		if (playbackQueue.CurrentTrack is not { } current)
		{
			playStatusPresenter.SetPlayStatus("Сначала начните воспроизведение трека");
			return;
		}

		string? tag = await ResolveTagAsync(current);
		if (tag is null)
		{
			playStatusPresenter.SetPlayStatus("У текущего трека нет жанра — радио по тегу не построить");
			return;
		}

		await ShowAsync(
			RadioTitle,
			$"По тегу «{tag}»",
			ct => listenBrainz.GetRadioByTagAsync(tag, ct),
			setWindowTitle);
	}

	private async Task ShowAsync(
		string title,
		string description,
		Func<CancellationToken, Task<IReadOnlyList<RecommendationTrack>>> load,
		Action<string> setWindowTitle)
	{
		try
		{
			playStatusPresenter.SetPlayStatus($"{title}: запрашиваем…");

			IReadOnlyList<RecommendationTrack> recommendations = await load(CancellationToken.None);
			if (recommendations.Count == 0)
			{
				playStatusPresenter.SetPlayStatus(
					$"{title}: сервис ничего не вернул (нужна история прослушиваний?)");
				return;
			}

			List<TrackKey> keys = recommendations
				.Where(track => track.IsResolved)
				.Select(track => track.Key!.Value)
				.Distinct()
				.ToList();

			if (keys.Count == 0)
			{
				playStatusPresenter.SetPlayStatus(
					$"{title}: ни один из {recommendations.Count} треков не найден в библиотеке");
				return;
			}

			loadStrategy.SetTrackKeys(keys);

			var playlist = new Playlist(ListenBrainzLoadStrategy.PlaylistKind, title)
			{
				SourceId = null, // плейлист ядра: стратегия уже знает ключи
				Category = PlaylistCategory.Transient,
				Description = description,
				TrackCount = keys.Count
			};

			await tracksPresenter.LoadTracksFor(playlist);
			playlistsPresenter.NotifyTransientPlaylistActive(playlist);
			playbackQueue.SetQueue(keys, 0);

			setWindowTitle($"{title} : {description}");
			playStatusPresenter.SetPlayStatus($"{title}: {keys.Count} из {recommendations.Count}");
			Logging.LogUserAction($"ListenBrainz: показано треков {keys.Count}");
		}
		catch (Exception exception)
		{
			// Меню работает в фоне: модальное окно здесь неуместно.
			Log.Warning(exception, "ListenBrainz: не удалось показать {Title}", title);
			playStatusPresenter.SetPlayStatus($"{title}: ошибка запроса, подробности в логе");
		}
	}

	private async Task<string?> ResolveTagAsync(TrackKey key)
	{
		try
		{
			Track track = await trackInfoProvider.GetTrackInfoById(key);
			return track.Genres?.FirstOrDefault(genre => !string.IsNullOrWhiteSpace(genre));
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Не удалось получить жанр трека {TrackKey}", key);
			return null;
		}
	}
}
