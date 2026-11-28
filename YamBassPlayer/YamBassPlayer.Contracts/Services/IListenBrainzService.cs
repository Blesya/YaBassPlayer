using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// ListenBrainz как сервис продукта: отправка прослушиваний и получение
/// рекомендаций. Резолв MBID в наши треки делает реализация, наружу приходят
/// уже готовые <see cref="RecommendationTrack"/>.
/// </summary>
public interface IListenBrainzService
{
	/// <summary>Заданы ли токен и имя пользователя (нужны персональным рекомендациям).</summary>
	bool IsConfigured { get; }

	/// <summary>Задан ли токен: без него не работают ни отправка, ни LB Radio.</summary>
	bool HasToken { get; }

	/// <summary>
	/// Подписка на прослушивания. Вызывается один раз при старте: без неё история
	/// не копится, а значит и рекомендации не появятся.
	/// </summary>
	void Start();

	/// <summary>Проверяет токен; возвращает имя пользователя или <see langword="null"/>.</summary>
	Task<string?> ValidateAsync(CancellationToken ct = default);

	/// <summary>
	/// Коллаборативные рекомендации. Пустой список — истории ещё нет или модель
	/// не пересчитана (сервер отвечает 204).
	/// </summary>
	Task<IReadOnlyList<RecommendationTrack>> GetRecommendationsAsync(int count = 25, CancellationToken ct = default);

	/// <summary>LB Radio по тегу: работает без истории и без MBID.</summary>
	Task<IReadOnlyList<RecommendationTrack>> GetRadioByTagAsync(string tag, CancellationToken ct = default);

	/// <summary>LB Radio по артисту: нужен MusicBrainz MBID, а не имя.</summary>
	Task<IReadOnlyList<RecommendationTrack>> GetRadioByArtistAsync(string artistMbid, CancellationToken ct = default);

	/// <summary>Отправляет одно прослушивание. Ошибки не пробрасываются наружу.</summary>
	Task SubmitListenAsync(TrackKey key, double playedSeconds, CancellationToken ct = default);

	/// <summary>
	/// Отправляет «сейчас играет»: сайт показывает это в Listening Now, в историю
	/// запись не попадает.
	/// </summary>
	Task SubmitPlayingNowAsync(TrackKey key, CancellationToken ct = default);

	/// <summary>Меняет настройки на ходу: вызывается после правки в меню «Настройки».</summary>
	void UpdateCredentials(string? userName, string? token);
}
