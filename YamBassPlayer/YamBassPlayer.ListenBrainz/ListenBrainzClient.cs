using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YamBassPlayer.ListenBrainz;

/// <summary>
/// HTTP-клиент ListenBrainz. Запросы сериализуются (API просит не чаще одного
/// в секунду), ошибки приходят типизированными, а «нет данных» — это
/// <see langword="null"/>, а не исключение: сервер отвечает 204, пока модель
/// не посчитала рекомендации.
/// </summary>
public sealed class ListenBrainzClient : IListenBrainzClient, IDisposable
{
	private const int MetadataBatchSize = 100;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private static readonly string ClientVersion =
		typeof(ListenBrainzClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

	/// <summary>Имя клиента в служебных полях: по нему ListenBrainz опознаёт «нашу» запись.</summary>
	private const string ClientName = "YamBassPlayer";

	private readonly HttpClient _http;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private ListenBrainzOptions _options;
	private DateTime _lastRequestUtc = DateTime.MinValue;
	private bool _disposed;

	/// <summary>
	/// Единственный конструктор: транспорт передаётся снаружи. Клиент намеренно
	/// не умеет создать <see cref="HttpClient"/> сам — иначе тест мог бы нечаянно
	/// уйти в настоящий API.
	/// </summary>
	public ListenBrainzClient(ListenBrainzOptions options, HttpClient httpClient)
	{
		_options = options;
		_http = httpClient;

		// TryAddWithoutValidation: User-Agent у MetaBrainz имеет вид
		// "Приложение/версия ( контакт )", и строгий парсер заголовков на нём спотыкается.
		_http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
		_http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
	}

	public string? UserName => _options.UserName;

	public bool HasToken => _options.HasToken;

	/// <summary>
	/// Меняет токен и имя пользователя на ходу: после правки в меню «Настройки»
	/// перезапуск не нужен.
	/// </summary>
	public void UpdateCredentials(string? userName, string? token)
		=> _options = _options with { UserName = userName, Token = token };

	public async Task<string?> ValidateTokenAsync(CancellationToken ct = default)
	{
		if (!HasToken)
			return null;

		using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "1/validate-token", null, ct);
		string body = await ReadOrThrowAsync(response, ct);
		ValidateTokenDto? dto = JsonSerializer.Deserialize<ValidateTokenDto>(body, JsonOptions);
		return dto is { Valid: true } ? dto.UserName : null;
	}

	public async Task SubmitListenAsync(ListenSubmission submission, CancellationToken ct = default)
	{
		await SendListenAsync("single", ToListenDto(submission, submission.StartedAt.ToUnixTimeSeconds()), ct);
	}

	/// <summary>
	/// «Сейчас играет»: транзиентная запись без <c>listened_at</c>. Сайт показывает её
	/// в Listening Now, а сама она в историю не попадает.
	/// </summary>
	public async Task SubmitPlayingNowAsync(ListenSubmission submission, CancellationToken ct = default)
	{
		await SendListenAsync("playing_now", ToListenDto(submission, listenedAt: null), ct);
	}

	/// <summary>
	/// Стирает «сейчас играет», но только свою запись: чужой плеер с той же учётки
	/// остаётся нетронутым. Без этого на сайте висит последний трек после остановки.
	/// </summary>
	public async Task ClearPlayingNowAsync(CancellationToken ct = default)
	{
		RequireToken();

		using HttpResponseMessage response = await SendAsync(
			HttpMethod.Post,
			"1/playing-now/delete",
			new ClearPlayingNowDto(ClientName),
			ct);
		await ReadOrThrowAsync(response, ct);
	}

	private async Task SendListenAsync(string listenType, ListenDto listen, CancellationToken ct)
	{
		RequireToken();

		var request = new SubmitListensDto(listenType, [listen]);
		using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "1/submit-listens", request, ct);
		await ReadOrThrowAsync(response, ct);
	}

	public async Task<RecommendationBatch?> GetRecommendationsAsync(
		string userName,
		int count,
		int offset = 0,
		CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(userName))
			throw new ArgumentException("Не задано имя пользователя ListenBrainz", nameof(userName));

		string url = $"1/cf/recommendation/user/{Uri.EscapeDataString(userName)}/recording?count={count}&offset={offset}";
		using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url, null, ct);

		// 204 — история есть, но рекомендаций пока нет. Это штатное состояние.
		if (response.StatusCode == HttpStatusCode.NoContent)
			return null;

		string body = await ReadOrThrowAsync(response, ct);
		CfResponseDto? dto = JsonSerializer.Deserialize<CfResponseDto>(body, JsonOptions);
		CfPayloadDto? payload = dto?.Payload;
		if (payload is null)
			return null;

		var items = (payload.Mbids ?? [])
			.Select(item => new RecommendationItem(item.RecordingMbid, item.Score, ParseTimestamp(item.LatestListenedAt)))
			.ToList();

		return new RecommendationBatch(items, payload.TotalMbidCount, FromUnixSeconds(payload.LastUpdated));
	}

	public async Task<IReadOnlyList<RecordingMetadata>> GetMetadataAsync(
		IReadOnlyCollection<string> recordingMbids,
		CancellationToken ct = default)
	{
		var mbids = recordingMbids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
		if (mbids.Count == 0)
			return [];

		var result = new List<RecordingMetadata>(mbids.Count);

		foreach (var chunk in mbids.Chunk(MetadataBatchSize))
		{
			string url = $"1/metadata/recording?recording_mbids={string.Join(",", chunk)}&inc=artist";
			using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url, null, ct);
			string body = await ReadOrThrowAsync(response, ct);

			Dictionary<string, MetadataEntryDto>? entries =
				JsonSerializer.Deserialize<Dictionary<string, MetadataEntryDto>>(body, JsonOptions);
			if (entries is null)
				continue;

			foreach (string mbid in chunk)
			{
				if (!entries.TryGetValue(mbid, out MetadataEntryDto? entry))
					continue;

				result.Add(new RecordingMetadata(
					mbid,
					entry.Artist?.Name ?? string.Empty,
					entry.Recording?.Name,
					entry.Recording?.Length is { } length ? (int?)length : null,
					entry.Recording?.Isrcs?.FirstOrDefault()));
			}
		}

		return result;
	}

	public async Task<RadioPlaylist?> GetRadioAsync(
		string prompt,
		RadioMode mode = RadioMode.Easy,
		CancellationToken ct = default)
	{
		RequireToken();

		string url = $"1/explore/lb-radio?prompt={Uri.EscapeDataString(prompt)}&mode={ModeName(mode)}";
		using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url, null, ct);
		string body = await ReadOrThrowAsync(response, ct);

		RadioResponseDto? dto = JsonSerializer.Deserialize<RadioResponseDto>(body, JsonOptions);
		JspfPlaylistDto? playlist = dto?.Payload?.Jspf?.Playlist;
		if (playlist is null)
			return null;

		var tracks = (playlist.Track ?? [])
			.Select(track => new RadioTrack(
				track.Title ?? string.Empty,
				track.Creator ?? string.Empty,
				track.Album,
				track.Duration is { } duration ? (int?)duration : null,
				MbIdFromIdentifier(track.Identifier)))
			.ToList();

		return new RadioPlaylist(
			playlist.Title ?? "LB Radio",
			tracks,
			dto?.Payload?.Feedback ?? []);
	}

	public async Task SendRecommendationFeedbackAsync(
		string recordingMbid,
		RecommendationRating rating,
		CancellationToken ct = default)
	{
		RequireToken();

		var request = new RecommendationFeedbackDto(recordingMbid, RatingName(rating));
		using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "1/recommendation/feedback/submit", request, ct);
		await ReadOrThrowAsync(response, ct);
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_gate.Dispose();
		_http.Dispose();
	}

	private async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string relativeUrl,
		object? body,
		CancellationToken ct)
	{
		await _gate.WaitAsync(ct);
		try
		{
			await WaitForSlotAsync(ct);

			using var request = new HttpRequestMessage(method, new Uri(_options.BaseUrl, relativeUrl));
			if (_options.HasToken)
				request.Headers.TryAddWithoutValidation("Authorization", $"Token {_options.Token}");

			if (body is not null)
				request.Content = new StringContent(
					JsonSerializer.Serialize(body, JsonOptions),
					Encoding.UTF8,
					"application/json");

			HttpResponseMessage response = await _http.SendAsync(request, ct);
			_lastRequestUtc = DateTime.UtcNow;
			return response;
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task WaitForSlotAsync(CancellationToken ct)
	{
		if (_options.MinRequestInterval <= TimeSpan.Zero || _lastRequestUtc == DateTime.MinValue)
			return;

		TimeSpan wait = _options.MinRequestInterval - (DateTime.UtcNow - _lastRequestUtc);
		if (wait > TimeSpan.Zero)
			await Task.Delay(wait, ct);
	}

	private static async Task<string> ReadOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
	{
		string body = await response.Content.ReadAsStringAsync(ct);
		if (response.IsSuccessStatusCode)
			return body;

		throw new ListenBrainzException((int)response.StatusCode, ExtractError(body));
	}

	/// <summary>Тело ошибки у ListenBrainz — <c>{"code":401,"error":"…"}</c>.</summary>
	private static string ExtractError(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
			return "(пустой ответ)";

		try
		{
			using JsonDocument document = JsonDocument.Parse(body);
			JsonElement root = document.RootElement;

			if (root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.String)
				return error.GetString() ?? body;

			if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
				return message.GetString() ?? body;
		}
		catch (JsonException)
		{
			// Не JSON — покажем как есть, обрезав до разумного.
		}

		return body.Length <= 300 ? body.Trim() : body[..300].Trim() + "…";
	}

	private void RequireToken()
	{
		if (!HasToken)
			throw new InvalidOperationException("Токен ListenBrainz не настроен");
	}

	private static ListenDto ToListenDto(ListenSubmission submission, long? listenedAt) => new(
		listenedAt,
		new TrackMetadataDto(
			submission.TrackName,
			submission.ArtistName,
			submission.ReleaseName,
			new AdditionalInfoDto(
				PositiveDuration(submission.DurationMs),
				PlayedMilliseconds(submission.PlayedSeconds),
				submission.Isrc,
				submission.RecordingMbid,
				MediaPlayer: ClientName,
				SubmissionClient: ClientName,
				SubmissionClientVersion: ClientVersion,
				MusicService: submission.MusicService,
				OriginUrl: submission.OriginUrl)));

	/// <summary>
	/// ListenBrainz отвечает 400 на нулевые длительности («should be a positive
	/// integer»), поэтому ноль превращается в отсутствие поля.
	/// </summary>
	private static int? PositiveDuration(int? durationMs) => durationMs is > 0 ? durationMs : null;

	private static int? PlayedMilliseconds(double? playedSeconds)
		=> playedSeconds is { } played && played > 0
			? (int)Math.Round(played * 1000)
			: null;

	private static string ModeName(RadioMode mode) => mode switch
	{
		RadioMode.Medium => "medium",
		RadioMode.Hard => "hard",
		_ => "easy"
	};

	private static string RatingName(RecommendationRating rating) => rating switch
	{
		RecommendationRating.Hate => "hate",
		RecommendationRating.BadRecommendation => "bad_recommendation",
		_ => "love"
	};

	private static string? MbIdFromIdentifier(List<string>? identifier)
	{
		string? url = identifier?.FirstOrDefault(id => id.Contains("/recording/", StringComparison.Ordinal));
		return url is null ? null : url[(url.LastIndexOf('/') + 1)..];
	}

	private static DateTimeOffset? FromUnixSeconds(long? value) =>
		value is > 0 ? DateTimeOffset.FromUnixTimeSeconds(value.Value) : null;

	private static DateTimeOffset? ParseTimestamp(string? value) =>
		DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : null;

	// ── DTO запросов ──────────────────────────────────────────────────────

	private sealed record SubmitListensDto(string ListenType, IReadOnlyList<ListenDto> Payload);

	private sealed record ListenDto(long? ListenedAt, TrackMetadataDto TrackMetadata);

	private sealed record TrackMetadataDto(
		string TrackName,
		string ArtistName,
		string? ReleaseName,
		AdditionalInfoDto AdditionalInfo);

	private sealed record AdditionalInfoDto(
		int? DurationMs,
		int? DurationPlayedMs,
		string? Isrc,
		string? RecordingMbid,
		string? MediaPlayer,
		string? SubmissionClient,
		string? SubmissionClientVersion,
		string? MusicService,
		string? OriginUrl);

	private sealed record RecommendationFeedbackDto(string RecordingMbid, string Rating);

	private sealed record ClearPlayingNowDto(string Client);

	// ── DTO ответов ───────────────────────────────────────────────────────

	private sealed record ValidateTokenDto(bool Valid, string? UserName, string? Message);

	private sealed record CfResponseDto(CfPayloadDto? Payload);

	private sealed record CfPayloadDto(
		int Count,
		int Offset,
		int TotalMbidCount,
		long? LastUpdated,
		List<CfItemDto>? Mbids);

	private sealed record CfItemDto(string RecordingMbid, double Score, string? LatestListenedAt);

	private sealed record MetadataEntryDto(MetadataArtistDto? Artist, MetadataRecordingDto? Recording);

	private sealed record MetadataArtistDto(string? Name);

	private sealed record MetadataRecordingDto(string? Name, List<string>? Isrcs, long? Length);

	private sealed record RadioResponseDto(RadioPayloadDto? Payload);

	private sealed record RadioPayloadDto(List<string>? Feedback, JspfDto? Jspf);

	private sealed record JspfDto(JspfPlaylistDto? Playlist);

	private sealed record JspfPlaylistDto(string? Title, List<JspfTrackDto>? Track);

	private sealed record JspfTrackDto(
		string? Title,
		string? Creator,
		string? Album,
		long? Duration,
		List<string>? Identifier);
}
