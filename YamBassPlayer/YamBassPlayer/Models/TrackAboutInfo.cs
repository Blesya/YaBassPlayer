namespace YamBassPlayer.Models;

/// <summary>
/// Данные для окна «О треке»: доступность, метки, кредиты и описание исполнителя.
/// Не кэшируется в БД — доступность и метки могут меняться.
/// </summary>
public sealed class TrackAboutInfo
{
	public required string TrackId { get; init; }
	public required string Title { get; init; }
	public required string Artist { get; init; }
	public string? Album { get; init; }

	public bool IsAvailable { get; init; } = true;
	public string? UnavailableReason { get; init; }
	public string? SubstituteTitle { get; init; }
	public string? Note { get; init; }

	public IReadOnlyList<TrackAboutLabel> Labels { get; init; } = [];
	public IReadOnlyList<TrackAboutCredit> Credits { get; init; } = [];
	public string? ArtistDescription { get; init; }
}

/// <summary>Метка трека: explicit, возраст, иноагент, причина блокировки и т.п.</summary>
public sealed record TrackAboutLabel(string Kind, string Text);

/// <summary>Участник создания трека.</summary>
public sealed record TrackAboutCredit(string Title, string Value);
