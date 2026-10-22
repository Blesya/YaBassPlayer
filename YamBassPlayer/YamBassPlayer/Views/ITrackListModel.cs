using YamBassPlayer.Models;

namespace YamBassPlayer.Views;

/// <summary>
/// Элемент списка треков: сам трек, предвычисленный номер для отображения
/// и состояние локального кэша (скачан / качается на N% / ошибка).
/// Их считает модель, вьюхи только рендерят.
/// </summary>
public readonly record struct TrackListItem(Track Track, string Number, TrackDownloadState Download);

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
	TrackKey? PlayingTrack { get; }
	int SelectedIndex { get; }
	int ScrollOffset { get; }
	bool IsLoadingMore { get; }

	void SetTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached);
	void AddTracks(IEnumerable<Track> tracks, Func<Track, bool> isCached);
	void Clear();
	void SetFilter(string? filter);
	void SetPlayingTrack(TrackKey? key);
	void SetLoadingMore(bool value);

	/// <summary>
	/// Обновляет состояние кэша одного трека. Список не пересобирается: меняется
	/// только строка, поэтому прогресс загрузки виден без перезагрузки плейлиста.
	/// </summary>
	void SetDownloadState(TrackKey key, TrackDownloadState state);

	bool MoveUp(int step);
	bool MoveDown(int step);
	bool PageUp(int pageSize);
	bool PageDown(int pageSize);
	bool Home();
	bool End();

	/// <summary>Ставит выделение на абсолютный индекс (клик мышью), с ограничением.</summary>
	bool Select(int index);

	/// <summary>Ставит выделение на трек с указанным id. false — такого трека в списке нет.</summary>
	bool SelectByTrack(TrackKey key);

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
