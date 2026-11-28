namespace YamBassPlayer.ListenBrainz;

/// <summary>
/// Настройки клиента ListenBrainz. Токен и имя пользователя необязательны:
/// без них доступны только анонимные запросы (CF-рекомендации, метаданные,
/// строительные блоки LB Radio).
/// </summary>
public sealed record ListenBrainzOptions
{
	public const string DefaultBaseUrl = "https://api.listenbrainz.org/";

	/// <summary>
	/// MetaBrainz требует User-Agent с контактом, иначе запросы режут как ботов.
	/// Формат: <c>Приложение/версия ( контакт )</c>.
	/// </summary>
	public const string DefaultUserAgent = "YamBassPlayer/2.3 ( https://github.com/ajata/YaBassPlayer )";

	public Uri BaseUrl { get; init; } = new(DefaultBaseUrl);

	public string? Token { get; init; }

	public string? UserName { get; init; }

	public string UserAgent { get; init; } = DefaultUserAgent;

	/// <summary>
	/// API просит не чаще одного запроса в секунду. Ноль отключает троттлинг:
	/// так делают тесты, чтобы не ждать.
	/// </summary>
	public TimeSpan MinRequestInterval { get; init; } = TimeSpan.FromSeconds(1);

	public bool HasToken => !string.IsNullOrWhiteSpace(Token);

	public bool HasCredentials => HasToken && !string.IsNullOrWhiteSpace(UserName);
}
