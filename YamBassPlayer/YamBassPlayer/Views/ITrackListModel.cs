using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

/// <summary>
/// Элемент списка треков: сам трек и предвычисленный номер для отображения
/// (с «*» у скачанных). Номер считает модель, вьюхи только рендерят.
/// </summary>
public readonly record struct TrackListItem(Track Track, string Number);

/// <summary>
/// Общая модель списка треков: данные хранятся один раз для таблицы и плиток.
/// Вьюхи подписываются на <see cref="Changed"/> и перерисовываются при изменении.
/// Индексы (<see cref="SelectedIndex"/>, <see cref="ScrollOffset"/>) — в элементах;
/// для сеточных вьюх используется шаг/stride, равный числу колонок.
/// </summary>
public interface ITrackListModel
{
	event Action? Changed;

	IReadOnlyList<TrackListItem> Items { get; }
	string? PlayingTrackId { get; }
	int SelectedIndex { get; }
	int ScrollOffset { get; }
	bool IsLoadingMore { get; }

	void SetTracks(IEnumerable<Track> tracks, Func<string, bool> isCached);
	void AddTracks(IEnumerable<Track> tracks, Func<string, bool> isCached);
	void Clear();
	void SetFilter(string? filter);
	void SetPlayingTrackId(string? trackId);
	void SetLoadingMore(bool value);

	bool MoveUp(int step);
	bool MoveDown(int step);
	bool PageUp(int pageSize);
	bool PageDown(int pageSize);
	bool Home();
	bool End();

	/// <summary>Ставит выделение на абсолютный индекс (клик мышью), с ограничением.</summary>
	bool Select(int index);

	/// <summary>Прокручивает список на <paramref name="delta"/> элементов, с ограничением.</summary>
	bool ScrollBy(int delta, int visibleCount);

	/// <summary>Держит выделенный элемент в видимой области (для сетки — по строкам).</summary>
	void EnsureSelectedVisible(int visibleCount, int stride = 1);

	/// <summary>
	/// true, если пора запросить следующую порцию; помечает загрузку, поэтому
	/// повторный вызов до сброса не сработает.
	/// </summary>
	bool ShouldRequestMore(int visibleCount);
}
