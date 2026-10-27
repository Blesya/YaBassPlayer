using YamBassPlayer.Enums;

namespace YamBassPlayer.Models;

/// <summary>
/// Плейлист источника или ядра. <see cref="Kind"/> — смысл, который знает только источник;
/// <see cref="Category"/> — политика ядра (хранить в снимке, пересобирать, держать до перезапуска).
/// </summary>
public class Playlist(string kind, string title)
{
	private string? _cacheKey;

	/// <summary><see langword="null"/> — плейлист ядра (очередь, топы), рисуется в корне дерева.</summary>
	public SourceId? SourceId { get; init; }

	/// <summary>Вид плейлиста внутри источника: <c>favorite</c>, <c>own</c>, <c>folder</c>, <c>artist</c>.</summary>
	public string Kind { get; } = kind;

	public string Title { get; } = title;

	public string Description { get; init; } = string.Empty;

	/// <summary>Подсказка для дерева, может быть неточной.</summary>
	public int TrackCount { get; set; }

	public PlaylistCategory Category { get; init; } = PlaylistCategory.Data;

	public PlaylistContext Context { get; init; } = PlaylistContext.Regular;

	/// <summary>Группа в дереве («Мои плейлисты», «Альбомы»); <see langword="null"/> — без группы.</summary>
	public string? Group { get; init; }

	/// <summary>Непрозрачные для ядра данные источника; участвуют только в <see cref="CacheKey"/>.</summary>
	public IReadOnlyDictionary<string, string>? Payload { get; init; }

	/// <summary>
	/// Ключ кэша: пара <c>owner</c>/<c>kind</c> из <see cref="Payload"/>, иначе «источник:вид:название».
	/// Вычисляется моделью; <c>init</c> позволяет закрепить ключ, пришедший из снимка.
	/// </summary>
	public string CacheKey
	{
		get => _cacheKey ?? ComputeCacheKey();
		init => _cacheKey = value;
	}

	/// <summary>
	/// Один и тот же плейлист: совпадают источник и ключ кэша. <see cref="Kind"/> в
	/// сравнение не входит: это подсказка загрузчику, а не идентичность. Источник вправе
	/// вернуть тот же плейлист с другим видом — например, сохранённый чужой стал
	/// собственным (<c>liked</c> → <c>own</c>), и прошлый выбор должен найтись.
	/// </summary>
	public bool HasSameIdentity(Playlist other)
		=> other is not null
		   && SourceId == other.SourceId
		   && string.Equals(CacheKey, other.CacheKey, StringComparison.Ordinal);

	public override string ToString()
	{
		return $"{Title} ({TrackCount})";
	}

	private string ComputeCacheKey()
	{
		if (Payload is not null
			&& Payload.TryGetValue("owner", out var owner)
			&& Payload.TryGetValue("kind", out var payloadKind)
			&& !string.IsNullOrEmpty(owner)
			&& !string.IsNullOrEmpty(payloadKind))
		{
			return $"{owner}:{payloadKind}";
		}

		return $"{SourceId?.Value}:{Kind}:{Title}";
	}
}
