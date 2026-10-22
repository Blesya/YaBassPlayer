using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Идентификатор источника Яндекс.Музыки — единственный владелец литерала.
/// </summary>
public static class YandexMusicSourceId
{
	public static readonly SourceId Value = new("yandex");
}
