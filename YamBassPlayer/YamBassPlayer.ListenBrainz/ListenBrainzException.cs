namespace YamBassPlayer.ListenBrainz;

/// <summary>Ошибка ListenBrainz: HTTP-статус и текст от сервера.</summary>
public sealed class ListenBrainzException(int statusCode, string serverMessage)
	: Exception($"ListenBrainz {statusCode}: {serverMessage}")
{
	public int StatusCode { get; } = statusCode;

	public string ServerMessage { get; } = serverMessage;

	/// <summary>Протухший токен — единственный случай, который требуют перевыпустить.</summary>
	public bool IsUnauthorized => StatusCode == 401;

	/// <summary>Превышен лимит запросов: повторять нужно позже, а не сразу.</summary>
	public bool IsRateLimited => StatusCode == 429;
}
