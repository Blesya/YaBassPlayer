namespace YamBassPlayer.ListenBrainz;

/// <summary>
/// Тонкая обёртка над HTTP API ListenBrainz. Никакой логики плеера здесь нет:
/// только запросы, разбор ответов и внятные ошибки.
/// </summary>
public interface IListenBrainzClient
{
	/// <summary>Имя пользователя из настроек; <see langword="null"/> — не задано.</summary>
	string? UserName { get; }

	bool HasToken { get; }

	/// <summary>Возвращает имя пользователя для валидного токена, иначе <see langword="null"/>.</summary>
	Task<string?> ValidateTokenAsync(CancellationToken ct = default);

	/// <summary>Отправляет одно прослушивание (<c>listen_type: single</c>).</summary>
	Task SubmitListenAsync(ListenSubmission submission, CancellationToken ct = default);

	/// <summary>
	/// Отправляет «сейчас играет» (<c>listen_type: playing_now</c>). Запись живёт
	/// временно и в историю не попадает — она только для Listening Now на сайте.
	/// </summary>
	Task SubmitPlayingNowAsync(ListenSubmission submission, CancellationToken ct = default);

	/// <summary>Стирает «сейчас играет», если оно отправлено этим клиентом.</summary>
	Task ClearPlayingNowAsync(CancellationToken ct = default);

	/// <summary>Меняет токен и имя пользователя без пересоздания клиента.</summary>
	void UpdateCredentials(string? userName, string? token);

	/// <summary>
	/// Коллаборативные рекомендации. <see langword="null"/> — сервер ответил 204:
	/// история есть, но модель ещё не посчитала рекомендации.
	/// </summary>
	Task<RecommendationBatch?> GetRecommendationsAsync(string userName, int count, int offset = 0, CancellationToken ct = default);

	/// <summary>Разрешает MBID в названия. Один запрос на пачку (до 100 MBID).</summary>
	Task<IReadOnlyList<RecordingMetadata>> GetMetadataAsync(IReadOnlyCollection<string> recordingMbids, CancellationToken ct = default);

	/// <summary>
	/// LB Radio по промпту (<c>tag:(…)</c> или <c>artist:(&lt;mbid&gt;)</c>).
	/// Требует токен; <see langword="null"/> — плейлист пустой или недоступен.
	/// </summary>
	Task<RadioPlaylist?> GetRadioAsync(string prompt, RadioMode mode = RadioMode.Easy, CancellationToken ct = default);

	/// <summary>
	/// Оценка рекомендации: обучает именно коллаборативную модель.
	/// Оценка записи (лайк/дизлайк) — это другой эндпоинт, здесь его нет.
	/// </summary>
	Task SendRecommendationFeedbackAsync(string recordingMbid, RecommendationRating rating, CancellationToken ct = default);
}

/// <summary>Значения поля <c>rating</c> у рекомендательного фидбека.</summary>
public enum RecommendationRating
{
	Love,
	Hate,
	BadRecommendation
}
