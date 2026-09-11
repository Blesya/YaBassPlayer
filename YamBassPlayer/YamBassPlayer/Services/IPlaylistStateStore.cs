using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

/// <summary>
/// Хранилище снимка состояния плейлистов между запусками приложения.
/// Загрузка выполняется синхронно, чтобы состояние можно было показать сразу при старте.
/// </summary>
public interface IPlaylistStateStore
{
	/// <summary>Читает снимок, сохранённый предыдущим запуском. Возвращает null, если его нет.</summary>
	PlaylistState? Load();

	/// <summary>Сохраняет снимок.</summary>
	void Save(PlaylistState state);
}
