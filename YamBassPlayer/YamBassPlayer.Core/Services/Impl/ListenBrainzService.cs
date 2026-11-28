using Serilog;
using YamBassPlayer.ListenBrainz;
using YamBassPlayer.Models;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// ListenBrainz в ядре: копит историю прослушиваний и превращает ответы сервиса
/// в наши <see cref="RecommendationTrack"/>. Сеть — только через
/// <see cref="IListenBrainzClient"/>; «половина трека или 4 минуты» из документации
/// не проверяется сервером, поэтому порог у нас свой — тот, что засчитывает
/// прослушивание (<see cref="ListenRecordedEvent"/>).
/// </summary>
public sealed class ListenBrainzService : IListenBrainzService
{
	/// <summary>Сколько рекомендаций просим по умолчанию: сервер отдаёт до 1000.</summary>
	public const int DefaultRecommendationCount = 25;

	private readonly IListenBrainzClient _client;
	private readonly ITrackInfoProvider _trackInfoProvider;
	private readonly ListenBrainzTrackResolver _resolver;
	private readonly IEventBus _eventBus;

	private bool _started;

	public ListenBrainzService(
		IListenBrainzClient client,
		ITrackInfoProvider trackInfoProvider,
		ListenBrainzTrackResolver resolver,
		IEventBus eventBus)
	{
		_client = client;
		_trackInfoProvider = trackInfoProvider;
		_resolver = resolver;
		_eventBus = eventBus;
	}

	public bool IsConfigured => _client.HasToken && !string.IsNullOrWhiteSpace(_client.UserName);

	public bool HasToken => _client.HasToken;

	public void UpdateCredentials(string? userName, string? token)
		=> _client.UpdateCredentials(userName, token);

	public void Start()
	{
		if (_started)
			return;

		_started = true;
		_eventBus.Subscribe<ListenRecordedEvent>(OnListenRecorded);
		_eventBus.Subscribe<TrackStartedEvent>(OnTrackStarted);
		_eventBus.Subscribe<PlaybackStateChangedEvent>(OnPlaybackStateChanged);
		Log.Information("ListenBrainz: подписка на прослушивания включена (настроен: {Configured})", IsConfigured);
	}

	public Task<string?> ValidateAsync(CancellationToken ct = default)
		=> _client.ValidateTokenAsync(ct);

	public async Task<IReadOnlyList<RecommendationTrack>> GetRecommendationsAsync(
		int count = DefaultRecommendationCount,
		CancellationToken ct = default)
	{
		if (!IsConfigured || _client.UserName is not { } userName)
			return [];

		RecommendationBatch? batch = await _client.GetRecommendationsAsync(userName, count, 0, ct);

		// null — сервер ответил 204: история есть, но модель ещё не посчитала.
		if (batch is null || batch.Items.Count == 0)
			return [];

		IReadOnlyList<RecordingMetadata> metadata =
			await _client.GetMetadataAsync(batch.Items.Select(item => item.RecordingMbid).ToList(), ct);
		Dictionary<string, RecordingMetadata> byMbid =
			metadata.ToDictionary(item => item.RecordingMbid, StringComparer.Ordinal);

		var result = new List<RecommendationTrack>(batch.Items.Count);
		foreach (RecommendationItem item in batch.Items)
		{
			byMbid.TryGetValue(item.RecordingMbid, out RecordingMetadata? meta);
			string artist = meta?.ArtistName ?? string.Empty;
			string title = meta?.TrackName ?? string.Empty;

			// Без названия сопоставлять нечего: строка была бы неиграбельной.
			if (title.Length == 0)
				continue;

			TrackKey? key = await _resolver.ResolveAsync(artist, title, ct);
			result.Add(new RecommendationTrack(
				item.RecordingMbid,
				artist,
				title,
				Album: null,
				meta?.DurationMs,
				item.Score,
				key));
		}

		Log.Information("ListenBrainz: рекомендаций получено {Found} из {Total}", result.Count, batch.Items.Count);
		return result;
	}

	public async Task<IReadOnlyList<RecommendationTrack>> GetRadioByTagAsync(
		string tag,
		CancellationToken ct = default)
	{
		string sanitized = tag.Replace("(", " ", StringComparison.Ordinal)
			.Replace(")", " ", StringComparison.Ordinal)
			.Trim();

		if (sanitized.Length == 0)
			return [];

		return await GetRadioAsync($"tag:({sanitized})", ct);
	}

	public Task<IReadOnlyList<RecommendationTrack>> GetRadioByArtistAsync(
		string artistMbid,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(artistMbid))
			return Task.FromResult<IReadOnlyList<RecommendationTrack>>([]);

		return GetRadioAsync($"artist:({artistMbid})", ct);
	}

	public async Task SubmitListenAsync(TrackKey key, double playedSeconds, CancellationToken ct = default)
	{
		if (!IsConfigured)
			return;

		try
		{
			ListenSubmission submission = await BuildSubmissionAsync(key, playedSeconds);
			await _client.SubmitListenAsync(submission, ct);
			Log.Debug("ListenBrainz: прослушивание отправлено ({Track})", key);
		}
		catch (ListenBrainzException exception) when (exception.IsRateLimited)
		{
			// Отправка прослушиваний — фоновая задача: не роняем плеер и не шумим.
			Log.Debug("ListenBrainz отклонил прослушивание по лимиту запросов");
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Не удалось отправить прослушивание в ListenBrainz");
		}
	}

	public async Task SubmitPlayingNowAsync(TrackKey key, CancellationToken ct = default)
	{
		if (!_client.HasToken)
			return;

		try
		{
			ListenSubmission submission = await BuildSubmissionAsync(key, playedSeconds: null);
			await _client.SubmitPlayingNowAsync(submission, ct);
			Log.Debug("ListenBrainz: «сейчас играет» отправлено ({Track})", key);
		}
		catch (ListenBrainzException exception) when (exception.StatusCode is 400 or 401)
		{
			// Наша ошибка в запросе или в токене не должна прятаться в Debug:
			// именно так «сейчас играет» молча не появлялось на сайте.
			Log.Warning(exception, "ListenBrainz отклонил «сейчас играет»");
		}
		catch (Exception exception)
		{
			// Сеть/таймаут — украшение, а не данные: не шумим.
			Log.Debug(exception, "ListenBrainz: не удалось отправить «сейчас играет»");
		}
	}

	/// <summary>
	/// Общее тело listen-а для обоих режимов: single и playing_now отличаются
	/// только наличием <c>listened_at</c>, который проставляет клиент.
	/// </summary>
	private async Task<ListenSubmission> BuildSubmissionAsync(TrackKey key, double? playedSeconds)
	{
		Track track = await _trackInfoProvider.GetTrackInfoById(key);

		// ListenBrainz ждёт время начала трека, а событие приходит уже после
		// того, как прослушивание засчитано.
		var startedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(Math.Max(0, playedSeconds ?? 0));

		return new ListenSubmission(
			startedAt,
			track.Artist,
			track.Title,
			string.IsNullOrWhiteSpace(track.Album) ? null : track.Album,
			track.DurationMs is { } durationMs ? (int)durationMs : null,
			playedSeconds,
			Isrc: null,
			RecordingMbid: null,
			MusicService: MusicServiceFor(key.SourceId),
			OriginUrl: null);
	}

	private async Task<IReadOnlyList<RecommendationTrack>> GetRadioAsync(string prompt, CancellationToken ct)
	{
		RadioPlaylist? playlist = await _client.GetRadioAsync(prompt, RadioMode.Easy, ct);
		if (playlist is null || playlist.Tracks.Count == 0)
			return [];

		var result = new List<RecommendationTrack>(playlist.Tracks.Count);
		foreach (RadioTrack track in playlist.Tracks)
		{
			if (track.Title.Length == 0)
				continue;

			TrackKey? key = await _resolver.ResolveAsync(track.Creator, track.Title, ct);
			result.Add(new RecommendationTrack(
				track.RecordingMbid ?? string.Empty,
				track.Creator,
				track.Title,
				track.Album,
				track.DurationMs,
				Score: 0,
				key));
		}

		Log.Information("ListenBrainz: LB Radio «{Prompt}» дал {Found} из {Total} играбельных",
			prompt, result.Count(track => track.IsResolved), playlist.Tracks.Count);
		return result;
	}

	/// <summary>
	/// Домен сервиса для поля <c>music_service</c>. Пока это литерал источника:
	/// типизированная идентичность источников — отдельный шаг (решение №2).
	/// </summary>
	private static string? MusicServiceFor(SourceId sourceId) => sourceId.Value switch
	{
		"yandex" => "music.yandex.ru",
		_ => null
	};

	private void OnListenRecorded(ListenRecordedEvent recorded)
	{
		if (!IsConfigured)
			return;

		// Событие приходит из таймера прослушивания — не блокируем его сетью.
		_ = Task.Run(() => SubmitListenAsync(recorded.Key, recorded.PlayedSeconds), CancellationToken.None);
	}

	private void OnTrackStarted(TrackStartedEvent started)
	{
		if (!_client.HasToken)
			return;

		// Сайт показывает «Listening now»: одна транзиентная запись на старт трека.
		_ = Task.Run(() => SubmitPlayingNowAsync(started.Key), CancellationToken.None);
	}

	private void OnPlaybackStateChanged(PlaybackStateChangedEvent changed)
	{
		// Остановка не заменяет запись новой, поэтому её надо стереть руками:
		// иначе на сайте висит последний трек, пока не истечёт само.
		if (changed.State != PlaybackState.Stopped || !_client.HasToken)
			return;

		_ = Task.Run(ClearPlayingNowAsync, CancellationToken.None);
	}

	private async Task ClearPlayingNowAsync()
	{
		try
		{
			await _client.ClearPlayingNowAsync();
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "ListenBrainz: не удалось стереть «сейчас играет»");
		}
	}
}
