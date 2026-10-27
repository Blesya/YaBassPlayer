using System.Threading;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Постоянное хранилище состава плейлистов (id треков по порядку). Нужно, чтобы
/// список треков открывался из локальной БД, не дожидаясь ответа источника.
/// Состав не входит в <c>playlist_state.json</c>: тот перезаписывается на каждом
/// старте трека, а состав меняется редко и бывает большим.
/// </summary>
public interface IPlaylistCompositionStore
{
	/// <summary>Возвращает сохранённый состав или <see langword="null"/>, если его нет.</summary>
	Task<IReadOnlyList<TrackKey>?> TryLoadAsync(string playlistKey, CancellationToken ct = default);

	/// <summary>Заменяет сохранённый состав плейлиста.</summary>
	Task SaveAsync(string playlistKey, IReadOnlyList<TrackKey> trackKeys, CancellationToken ct = default);

	/// <summary>Удаляет составы указанных плейлистов.</summary>
	Task RemoveAsync(IEnumerable<string> playlistKeys, CancellationToken ct = default);

	/// <summary>Ключи всех сохранённых составов — для чистки исчезнувших плейлистов.</summary>
	Task<IReadOnlyCollection<string>> GetKeysAsync(CancellationToken ct = default);
}
